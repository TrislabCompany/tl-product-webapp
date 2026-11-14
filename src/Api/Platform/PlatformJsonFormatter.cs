using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Api.Platform;

/// <summary>What every log line says about where it comes from.</summary>
public sealed class PlatformJsonFormatterOptions : ConsoleFormatterOptions
{
    public string Environment { get; set; } = PlatformConfiguration.LocalEnvironment;

    public string? Stamp { get; set; }

    public string Version { get; set; } = PlatformConfiguration.AppVersion;
}

/// <summary>
/// Writes each log entry as one JSON object on one line, with the fields of
/// the platform's log contract (ADR 0062 in tl-cloud-infrastructure, the Logs
/// section of tl-platform's stamps/aca-app/README.md): timestamp, level,
/// message, messageTemplate, category, trace_id, span_id, environment, stamp,
/// version and exception, then the message's named properties and the scopes'
/// properties in camelCase. One query then finds every line of a request in
/// any product.
/// </summary>
public sealed class PlatformJsonFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "platform-json";

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // The contract's own fields, and the scope keys the trace fields replace.
    private static readonly HashSet<string> Reserved =
    [
        "timestamp", "level", "message", "messageTemplate", "category", "trace_id", "span_id",
        "environment", "stamp", "version", "exception", "traceId", "spanId", "parentId",
    ];

    private readonly IDisposable? _reload;
    private PlatformJsonFormatterOptions _options;

    public PlatformJsonFormatter(IOptionsMonitor<PlatformJsonFormatterOptions> options)
        : base(FormatterName)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.CurrentValue;
        _reload = options.OnChange(o => _options = o);
    }

    public void Dispose() => _reload?.Dispose();

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
        {
            return;
        }

        var options = _options;
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            json.WriteString("level", logEntry.LogLevel.ToString());
            json.WriteString("message", message);

            var properties = logEntry.State as IReadOnlyList<KeyValuePair<string, object?>>;
            var template = properties?.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value as string;
            json.WriteString("messageTemplate", template ?? message);
            json.WriteString("category", logEntry.Category);

            if (Activity.Current is { } activity)
            {
                json.WriteString("trace_id", activity.TraceId.ToHexString());
                json.WriteString("span_id", activity.SpanId.ToHexString());
            }

            json.WriteString("environment", options.Environment);
            if (!string.IsNullOrEmpty(options.Stamp))
            {
                json.WriteString("stamp", options.Stamp);
            }
            json.WriteString("version", options.Version);

            if (logEntry.Exception is { } exception)
            {
                json.WriteStartObject("exception");
                json.WriteString("type", exception.GetType().FullName);
                json.WriteString("message", exception.Message);
                // ToString has the stack trace and every inner exception.
                json.WriteString("stack", exception.ToString());
                json.WriteEndObject();
            }

            var written = new HashSet<string>(Reserved, StringComparer.Ordinal);
            WriteProperties(json, properties, written);
            scopeProvider?.ForEachScope((scope, state) => WriteProperties(state.json, scope as IEnumerable<KeyValuePair<string, object?>>, state.written), (json, written));

            json.WriteEndObject();
        }

        textWriter.Write(Encoding.UTF8.GetString(buffer.WrittenSpan));
        textWriter.Write('\n');
    }

    private static void WriteProperties(Utf8JsonWriter json, IEnumerable<KeyValuePair<string, object?>>? properties, HashSet<string> written)
    {
        if (properties is null)
        {
            return;
        }

        foreach (var (key, value) in properties)
        {
            if (key.Length == 0 || key[0] == '{')
            {
                continue;
            }

            var name = char.ToLowerInvariant(key[0]) + key[1..];
            // A property with the name of a contract field, or one already
            // written, keeps the contract's field and the first value.
            if (written.Add(name))
            {
                json.WritePropertyName(name);
                WriteValue(json, value);
            }
        }
    }

    private static void WriteValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                break;
            case bool b:
                json.WriteBooleanValue(b);
                break;
            case int or long or short or byte or uint or ulong or ushort or sbyte or decimal:
                json.WriteRawValue(Convert.ToString(value, CultureInfo.InvariantCulture)!);
                break;
            case double d when double.IsFinite(d):
                json.WriteNumberValue(d);
                break;
            case float f when float.IsFinite(f):
                json.WriteNumberValue(f);
                break;
            case DateTime dateTime:
                json.WriteStringValue(dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                json.WriteStringValue(dateTimeOffset);
                break;
            case Guid guid:
                json.WriteStringValue(guid);
                break;
            default:
                json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
