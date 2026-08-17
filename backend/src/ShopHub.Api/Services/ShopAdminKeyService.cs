using System.Net;
using System.Text;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public class ShopAdminKeyService(IKubernetes client, IOptions<KubernetesOptions> options) : IShopAdminKeyService
{
    // Must match shophub-shop-operator's own naming (envFor's admin-key Secret) exactly —
    // there's no cross-repo shared constant, so keep the two in sync by hand if either changes.
    private const string SecretKey = "apiKey";

    private string Namespace => options.Value.Namespace;

    public async Task<string> GetAdminKeyAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        var secretName = $"{site.K8sName}-admin-key";

        k8s.Models.V1Secret secret;
        try
        {
            secret = await client.CoreV1.ReadNamespacedSecretAsync(secretName, Namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Admin key secret '{secretName}' not found in namespace '{Namespace}' — the operator may not have provisioned it yet.");
        }

        if (secret.Data is null || !secret.Data.TryGetValue(SecretKey, out var bytes))
        {
            throw new InvalidOperationException($"Admin key secret '{secretName}' is missing the '{SecretKey}' key.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
