namespace ShopHub.Api.Services;

public class KubernetesOptions
{
    public const string SectionName = "Kubernetes";

    /// <summary>Namespace that all Shop/Wallet/DiscordChannel custom resources are created in.</summary>
    public string Namespace { get; set; } = "shops";

    /// <summary>
    /// Use the in-cluster service account instead of a local kubeconfig. False for local
    /// dev (reads ~/.kube/config, same as kubectl); true once actually deployed to a cluster.
    /// </summary>
    public bool InCluster { get; set; }

    /// <summary>
    /// Host a shop's NodePort Service is reachable at — from wherever "Open site" gets clicked
    /// (the browser), not from this app's own pod. E.g. a node's external IP, a load balancer,
    /// or "localhost" for Docker Desktop's local Kubernetes. No sensible universal default.
    /// </summary>
    public string ExternalHost { get; set; } = "";
}
