using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ShopHub.Api.Observability;

public static class ObservabilityExtensions
{
    public const string ServiceName = "shophub-app-api";

    /// <summary>
    /// Wires up metrics (ASP.NET Core + HttpClient + EF Core + the custom traffic/visitor
    /// metrics above, exposed at /metrics for Prometheus to scrape), tracing (same
    /// instrumentation, exported via OTLP if <c>Observability:OtlpEndpoint</c> is
    /// configured — otherwise to the console, so tracing is visible without needing a
    /// collector running locally), and structured (JSON) logging correlated with traces.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"];

        builder.Services.AddSingleton<TrafficMetrics>();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(TrafficMetrics.MeterName)
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
                else
                {
                    // No collector configured — still make traces visible locally.
                    tracing.AddConsoleExporter();
                }
            });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                logging.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            }
        });

        // JSON lines on stdout regardless of OTLP config, so logs are always structured
        // (parseable by Loki/Fluent Bit/etc.) rather than freeform text.
        builder.Logging.AddJsonConsole();

        return builder;
    }
}
