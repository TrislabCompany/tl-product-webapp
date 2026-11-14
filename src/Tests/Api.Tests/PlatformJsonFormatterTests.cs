using System.Diagnostics;
using System.Text.Json;
using Api.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class PlatformJsonFormatterTests
{
    private static readonly PlatformJsonFormatterOptions Options = new() { Environment = "dev", Stamp = "shr-con-dev", Version = "1.2.3" };

    [Fact]
    public void Writes_one_json_line_with_the_contract_fields()
    {
        var line = Write(LogLevel.Information, "Item {ItemId} renamed to {Done}", [new("ItemId", 42), new("Done", true)], "Item 42 renamed to True");

        Assert.EndsWith("\n", line, StringComparison.Ordinal);
        Assert.Single(line.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        var json = JsonDocument.Parse(line).RootElement;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", json.GetProperty("timestamp").GetString());
        Assert.Equal("Information", json.GetProperty("level").GetString());
        Assert.Equal("Item 42 renamed to True", json.GetProperty("message").GetString());
        Assert.Equal("Item {ItemId} renamed to {Done}", json.GetProperty("messageTemplate").GetString());
        Assert.Equal("Api.Features.Items", json.GetProperty("category").GetString());
        Assert.Equal("dev", json.GetProperty("environment").GetString());
        Assert.Equal("shr-con-dev", json.GetProperty("stamp").GetString());
        Assert.Equal("1.2.3", json.GetProperty("version").GetString());
        Assert.Equal(42, json.GetProperty("itemId").GetInt32());
        Assert.True(json.GetProperty("done").GetBoolean());
        Assert.False(json.TryGetProperty("exception", out _));
    }

    [Fact]
    public void Writes_the_trace_and_span_of_the_current_activity()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();

        var json = JsonDocument.Parse(Write(LogLevel.Warning, "Retrying", [], "Retrying")).RootElement;

        Assert.Equal(activity.TraceId.ToHexString(), json.GetProperty("trace_id").GetString());
        Assert.Equal(activity.SpanId.ToHexString(), json.GetProperty("span_id").GetString());
    }

    [Fact]
    public void Writes_the_exception_with_its_type_message_and_stack()
    {
        var exception = Throw();

        var json = JsonDocument.Parse(Write(LogLevel.Error, "Import failed", [], "Import failed", exception)).RootElement;

        var written = json.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", written.GetProperty("type").GetString());
        Assert.Equal("The file is empty", written.GetProperty("message").GetString());
        Assert.Contains(nameof(Throw), written.GetProperty("stack").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Adds_the_scope_properties_but_keeps_the_contract_fields()
    {
        var scopes = new LoggerExternalScopeProvider();
        using var scope = scopes.Push(new Dictionary<string, object?> { ["RequestPath"] = "/api/items", ["TraceId"] = "from-the-scope", ["Level"] = "x" });

        var json = JsonDocument.Parse(Write(LogLevel.Information, "Listed", [], "Listed", scopes: scopes)).RootElement;

        Assert.Equal("/api/items", json.GetProperty("requestPath").GetString());
        Assert.False(json.TryGetProperty("traceId", out _));
        Assert.Equal("Information", json.GetProperty("level").GetString());
    }

    [Fact]
    public void Leaves_out_the_stamp_when_there_is_none()
    {
        var json = JsonDocument.Parse(Write(LogLevel.Information, "Started", [], "Started", options: new() { Environment = "test" })).RootElement;

        Assert.False(json.TryGetProperty("stamp", out _));
        Assert.Equal("test", json.GetProperty("environment").GetString());
    }

    private static string Write(
        LogLevel level,
        string template,
        List<KeyValuePair<string, object?>> properties,
        string message,
        Exception? exception = null,
        IExternalScopeProvider? scopes = null,
        PlatformJsonFormatterOptions? options = null)
    {
        properties.Add(new("{OriginalFormat}", template));
        using var formatter = new PlatformJsonFormatter(new StaticOptions(options ?? Options));
        using var writer = new StringWriter();
        IReadOnlyList<KeyValuePair<string, object?>> state = properties;
        formatter.Write(new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(level, "Api.Features.Items", default, state, exception, (_, _) => message), scopes, writer);
        return writer.ToString();
    }

    private static InvalidOperationException Throw()
    {
        try
        {
            throw new InvalidOperationException("The file is empty");
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private sealed class StaticOptions(PlatformJsonFormatterOptions value) : IOptionsMonitor<PlatformJsonFormatterOptions>
    {
        public PlatformJsonFormatterOptions CurrentValue => value;

        public PlatformJsonFormatterOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<PlatformJsonFormatterOptions, string?> listener) => null;
    }
}
