using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;

namespace ShopHub.Api.Observability;

/// <summary>
/// Custom metrics that aren't part of ASP.NET Core's built-in HTTP instrumentation:
/// request/response byte volume, and an approximate unique-visitor count. Same instrumentation
/// as shophub-shop's own TrafficMetrics, mirrored here so the new platform dashboard can reuse
/// its exact PromQL/metric names.
///
/// This is in-memory and per-instance: it resets on restart and doesn't merge across replicas
/// if autoscaling.enabled ever brings this above one — out of scope here, but worth knowing
/// before trusting this number under more than one replica.
/// </summary>
public sealed class TrafficMetrics : IDisposable
{
    public const string MeterName = "ShopHub.Api.Traffic";

    private readonly Meter _meter;
    private readonly Counter<long> _requestBytes;
    private readonly Counter<long> _responseBytes;
    private readonly ConcurrentDictionary<string, byte> _visitorsToday = new();
    private DateOnly _currentDay = DateOnly.FromDateTime(DateTime.UtcNow);

    public TrafficMetrics()
    {
        _meter = new Meter(MeterName);
        _requestBytes = _meter.CreateCounter<long>(
            "http.traffic.request_bytes", unit: "By", description: "Total bytes received across all HTTP requests.");
        _responseBytes = _meter.CreateCounter<long>(
            "http.traffic.response_bytes", unit: "By", description: "Total bytes sent across all HTTP responses.");
        _meter.CreateObservableGauge(
            "http.traffic.unique_visitors_today", ObserveUniqueVisitors,
            description: "Approximate distinct visitors seen since UTC midnight (hashed IP+User-Agent, in-memory, single-instance).");
    }

    public void RecordRequestBytes(long bytes) => _requestBytes.Add(bytes);

    public void RecordResponseBytes(long bytes) => _responseBytes.Add(bytes);

    public void RecordVisitor(string ipAddress, string userAgent)
    {
        RollDayIfNeeded();
        var key = $"{ipAddress}|{userAgent}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        _visitorsToday[hash] = 0;
    }

    private int ObserveUniqueVisitors()
    {
        RollDayIfNeeded();
        return _visitorsToday.Count;
    }

    private void RollDayIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _currentDay)
        {
            _visitorsToday.Clear();
            _currentDay = today;
        }
    }

    public void Dispose() => _meter.Dispose();
}
