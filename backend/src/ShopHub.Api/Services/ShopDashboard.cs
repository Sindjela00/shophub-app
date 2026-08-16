namespace ShopHub.Api.Services;

using ShopHub.Api.Models;

/// <summary>
/// A single-shop variant of shophub-kube-state's `$shop`-templated "ShopHub / Per-Shop
/// Overview" dashboard (clusters/local/shophub/values.yaml) — same panels and PromQL, but with
/// the shop baked in directly instead of a variable, since Grafana folder permissions restrict
/// access to a whole dashboard, not to specific values of a variable inside it. Provisioned one
/// per shop, into that shop's own folder, so a Viewer scoped to that folder sees only this.
/// </summary>
public static class ShopDashboard
{
    private static readonly object Datasource = new { type = "prometheus", uid = "Prometheus" };

    public static object Build(ShopSite site)
    {
        var shop = site.K8sName;

        return new
        {
            // Same uid as the folder itself (site.K8sName, "shop-{Id:N}") — Grafana uids are
            // capped at 40 characters, and a "-overview" suffix pushed this over that limit.
            // Folder and dashboard uids live in separate namespaces so reusing the value is
            // safe, and every shop has exactly one dashboard so there's nothing to disambiguate.
            uid = shop,
            title = $"{site.Name} — Overview",
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
                            expr = $"sum(rate(container_cpu_usage_seconds_total{{namespace=\"shops\", container!=\"\", container!=\"POD\"}}[5m]) * on(namespace,pod) group_left() kube_pod_labels{{namespace=\"shops\", label_app_kubernetes_io_instance=\"{shop}\"}})",
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
                            expr = $"sum(container_memory_working_set_bytes{{namespace=\"shops\", container!=\"\", container!=\"POD\"}} * on(namespace,pod) group_left() kube_pod_labels{{namespace=\"shops\", label_app_kubernetes_io_instance=\"{shop}\"}})",
                            legendFormat = "working set",
                        },
                    },
                },
                new
                {
                    id = 3,
                    type = "timeseries",
                    title = "Filesystem Usage",
                    gridPos = new { x = 12, y = 0, w = 6, h = 8 },
                    fieldConfig = new { defaults = new { unit = "bytes" }, overrides = Array.Empty<object>() },
                    targets = new[]
                    {
                        new
                        {
                            datasource = Datasource,
                            expr = $"sum(container_fs_usage_bytes{{namespace=\"shops\", container!=\"\", container!=\"POD\"}} * on(namespace,pod) group_left() kube_pod_labels{{namespace=\"shops\", label_app_kubernetes_io_instance=\"{shop}\"}})",
                            legendFormat = "used",
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
                            expr = $"sum(rate(container_network_receive_bytes_total{{namespace=\"shops\"}}[5m]) * on(namespace,pod) group_left() kube_pod_labels{{namespace=\"shops\", label_app_kubernetes_io_instance=\"{shop}\"}})",
                            legendFormat = "receive",
                        },
                        new
                        {
                            datasource = Datasource,
                            expr = $"sum(rate(container_network_transmit_bytes_total{{namespace=\"shops\"}}[5m]) * on(namespace,pod) group_left() kube_pod_labels{{namespace=\"shops\", label_app_kubernetes_io_instance=\"{shop}\"}})",
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
                            expr = $"sum(increase(http_server_request_duration_seconds_count{{namespace=\"shops\", service=\"{shop}\"}}[24h]))",
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
                            expr = $"sum(increase(http_server_request_duration_seconds_count{{namespace=\"shops\", service=\"{shop}\", http_response_status_code=~\"2..\"}}[24h]))",
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
                            expr = $"sum(increase(http_server_request_duration_seconds_count{{namespace=\"shops\", service=\"{shop}\", http_response_status_code=~\"4..|5..\"}}[24h]))",
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
                            expr = $"max(http_traffic_unique_visitors_today{{namespace=\"shops\", service=\"{shop}\"}})",
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
                            expr = $"sum(increase(http_traffic_request_bytes_total{{namespace=\"shops\", service=\"{shop}\"}}[24h]) + increase(http_traffic_response_bytes_total{{namespace=\"shops\", service=\"{shop}\"}}[24h])) / 1e9",
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
                            expr = $"sum by (http_route) (increase(http_server_request_duration_seconds_count{{namespace=\"shops\", service=\"{shop}\", http_response_status_code=\"404\"}}[24h]))",
                            format = "table",
                            instant = true,
                        },
                    },
                },
            },
        };
    }
}
