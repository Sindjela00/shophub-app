namespace ShopHub.Api.Services;

/// <summary>
/// shophub-app's own dashboard — CPU/RAM/network/HTTP-traffic for the ShopHub platform itself,
/// as opposed to <see cref="ShopDashboard"/> which is one-per-shop. A single, shared instance:
/// no per-shop templating, `namespace="shophub"` filtering is enough on its own to isolate
/// this workload's metrics (unlike the "shops" namespace, which holds every shop and needs the
/// extra instance-label join ShopDashboard does).
/// </summary>
public static class PlatformDashboard
{
    // Must match the uid kube-prometheus-stack's own datasource provisioning gives its
    // Prometheus datasource — see ShopDashboard's identical constant for the story here.
    private static readonly object Datasource = new { type = "prometheus", uid = "prometheus" };

    public static object Build()
    {
        return new
        {
            uid = "shophub-platform",
            title = "ShopHub — Platform Overview",
            tags = new[] { "shophub" },
            timezone = "browser",
            schemaVersion = 39,
            version = 1,
            refresh = "30s",
            time = new { from = "now-24h", to = "now" },
            panels = new object[]
            {
                new
                {
                    id = 1,
                    type = "timeseries",
                    title = "CPU Usage",
                    gridPos = new { x = 0, y = 0, w = 6, h = 8 },
                    fieldConfig = new { defaults = new { unit = "short" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(rate(container_cpu_usage_seconds_total{namespace=\"shophub\"}[5m]))",
                            legendFormat = "cores",
                        },
                    },
                },
                new
                {
                    id = 2,
                    type = "timeseries",
                    title = "Memory Usage",
                    gridPos = new { x = 6, y = 0, w = 6, h = 8 },
                    fieldConfig = new { defaults = new { unit = "bytes" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(container_memory_working_set_bytes{namespace=\"shophub\"})",
                            legendFormat = "working set",
                        },
                    },
                },
                new
                {
                    id = 3,
                    type = "timeseries",
                    title = "Disk I/O",
                    gridPos = new { x = 12, y = 0, w = 6, h = 8 },
                    fieldConfig = new { defaults = new { unit = "Bps" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(rate(container_fs_reads_bytes_total{namespace=\"shophub\"}[5m]))",
                            legendFormat = "read",
                        },
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(rate(container_fs_writes_bytes_total{namespace=\"shophub\"}[5m]))",
                            legendFormat = "write",
                        },
                    },
                },
                new
                {
                    id = 4,
                    type = "timeseries",
                    title = "Network I/O",
                    gridPos = new { x = 18, y = 0, w = 6, h = 8 },
                    fieldConfig = new { defaults = new { unit = "Bps" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(rate(container_network_receive_bytes_total{namespace=\"shophub\"}[5m]))",
                            legendFormat = "receive",
                        },
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(rate(container_network_transmit_bytes_total{namespace=\"shophub\"}[5m]))",
                            legendFormat = "transmit",
                        },
                    },
                },
                new
                {
                    id = 5,
                    type = "stat",
                    title = "Total Requests (24h)",
                    gridPos = new { x = 0, y = 8, w = 4, h = 4 },
                    fieldConfig = new { defaults = new { unit = "short" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(increase({\"http.server.request.duration_seconds_count\", namespace=\"shophub\"}[24h]))",
                        },
                    },
                },
                new
                {
                    id = 6,
                    type = "stat",
                    title = "Successful Requests (24h)",
                    gridPos = new { x = 4, y = 8, w = 4, h = 4 },
                    fieldConfig = new { defaults = new { unit = "short" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(increase({\"http.server.request.duration_seconds_count\", namespace=\"shophub\", \"http.response.status_code\"=~\"2..\"}[24h]))",
                        },
                    },
                },
                new
                {
                    id = 7,
                    type = "stat",
                    title = "Failed Requests (24h)",
                    gridPos = new { x = 8, y = 8, w = 4, h = 4 },
                    fieldConfig = new { defaults = new { unit = "short" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(increase({\"http.server.request.duration_seconds_count\", namespace=\"shophub\", \"http.response.status_code\"=~\"4..|5..\"}[24h]))",
                        },
                    },
                },
                new
                {
                    id = 8,
                    type = "stat",
                    title = "Unique Visitors Today",
                    gridPos = new { x = 12, y = 8, w = 4, h = 4 },
                    fieldConfig = new { defaults = new { unit = "short" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "max({\"http.traffic.unique_visitors_today\", namespace=\"shophub\"})",
                        },
                    },
                },
                new
                {
                    id = 9,
                    type = "stat",
                    title = "Total Traffic (24h)",
                    gridPos = new { x = 16, y = 8, w = 8, h = 4 },
                    fieldConfig = new { defaults = new { unit = "decgbytes" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum(increase({\"http.traffic.request_bytes_total\", namespace=\"shophub\"}[24h]) + increase({\"http.traffic.response_bytes_total\", namespace=\"shophub\"}[24h])) / 1e9",
                        },
                    },
                },
                new
                {
                    id = 10,
                    type = "table",
                    title = "404s by Endpoint (24h)",
                    gridPos = new { x = 0, y = 12, w = 24, h = 8 },
                    fieldConfig = new { defaults = new { }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = "sum by (\"http.route\") (increase({\"http.server.request.duration_seconds_count\", namespace=\"shophub\", \"http.response.status_code\"=\"404\"}[24h]))",
                            format = "table",
                            instant = true,
                        },
                    },
                },
            },
        };
    }
}
