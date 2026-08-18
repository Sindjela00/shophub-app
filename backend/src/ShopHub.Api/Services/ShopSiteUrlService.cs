using System.Net;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public class ShopSiteUrlService(IKubernetes client, IOptions<KubernetesOptions> options) : IShopSiteUrlService
{
    private string Namespace => options.Value.Namespace;

    public async Task<string> GetSiteUrlAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        var externalHost = options.Value.ExternalHost;
        if (string.IsNullOrWhiteSpace(externalHost))
        {
            throw new InvalidOperationException("Kubernetes:ExternalHost is not configured.");
        }

        k8s.Models.V1Service service;
        try
        {
            service = await client.CoreV1.ReadNamespacedServiceAsync(site.K8sName, Namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Service '{site.K8sName}' not found in namespace '{Namespace}'.");
        }

        // "http" must match the port name shop_controller.go's reconcileService gives it — no
        // shared constant across repos, keep in sync by hand (same caveat as ShopAdminKeyService's
        // SecretKey).
        var port = service.Spec?.Ports?.FirstOrDefault(p => p.Name == "http");
        if (port is null || port.NodePort is null or 0)
        {
            throw new InvalidOperationException($"Service '{site.K8sName}' has no allocated NodePort yet.");
        }

        return $"http://{externalHost}:{port.NodePort}/";
    }
}
