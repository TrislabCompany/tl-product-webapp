using System.Diagnostics;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Platform;

/// <summary>
/// Logs, the correlation id and traces as the platform's log contract says
/// (ADR 0062 in tl-cloud-infrastructure, .claude/rules/logging.md). Call after
/// AddPlatformSettings, which sets the Platform:* keys read here.
/// </summary>
public static class PlatformObservability
{
    /// <summary>The response header that carries the correlation id.</summary>
    public const string CorrelationHeader = "X-Correlation-ID";

    /// <summary>
    /// JSON lines on stdout (PlatformJsonFormatter) in every environment but
    /// local, which keeps the readable console lines.
    /// </summary>
    public static void AddPlatformLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var environment = builder.Configuration["Platform:Environment"] ?? PlatformConfiguration.LocalEnvironment;
        if (environment == PlatformConfiguration.LocalEnvironment)
        {
            return;
        }

        builder.Logging.AddConsole(options => options.FormatterName = PlatformJsonFormatter.FormatterName);
        builder.Logging.AddConsoleFormatter<PlatformJsonFormatter, PlatformJsonFormatterOptions>(options =>
        {
            options.Environment = environment;
            options.Stamp = builder.Configuration["Platform:Stamp"];
        });
    }

    /// <summary>
    /// Traces and metrics with OpenTelemetry, exported over OTLP only when
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set. Until the platform sets it, the app
    /// exports nothing and the JSON logs are the record.
    /// </summary>
    public static void AddPlatformTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(builder.Configuration["Platform:OtlpEndpoint"]))
        {
            return;
        }

        var attributes = new Dictionary<string, object>
        {
            ["deployment.environment.name"] = builder.Configuration["Platform:Environment"] ?? PlatformConfiguration.LocalEnvironment,
        };
        if (builder.Configuration["Platform:Stamp"] is { Length: > 0 } stamp)
        {
            attributes["platform.stamp"] = stamp;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: PlatformConfiguration.AppVersion)
                .AddAttributes(attributes))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsHealthProbe(context.Request.Path))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http", "System.Runtime", "Npgsql"))
            .UseOtlpExporter();
    }

    /// <summary>
    /// The trace id of the request: from the caller's W3C traceparent header
    /// when it sent one, else new. Support finds every log line of the request
    /// from it.
    /// </summary>
    public static string CorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
    }

    /// <summary>
    /// Returns the correlation id on every response as X-Correlation-ID. Add it
    /// first, so the header survives the exception handler.
    /// </summary>
    public static void UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var id = CorrelationId(context);
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeader] = id;
                return Task.CompletedTask;
            });
            await next(context);
        });

    /// <summary>Puts the correlation id in every ProblemDetails response as traceId.</summary>
    public static void AddPlatformProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = CorrelationId(context.HttpContext));

    private static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments("/healthz") || path.StartsWithSegments("/readyz");
}
