namespace ShopHub.Api.Services;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>The bot's Discord application client id — public, not secret (it's baked into
    /// every invite link a shop owner is handed). Used to build the invite-URL a shop owner
    /// follows to add the bot to their own server.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Name of the Secret (in <see cref="KubernetesOptions.Namespace"/>, the same
    /// namespace shophub-shop-operator's own DiscordChannel controller reads it from) holding
    /// the bot token — read live via the Kubernetes API rather than injected into this app's own
    /// env, so there's exactly one place the token is configured.</summary>
    public string BotTokenSecretName { get; set; } = "shop-operator-discord";

    public string BotTokenSecretKey { get; set; } = "DISCORD_BOT_TOKEN";
}
