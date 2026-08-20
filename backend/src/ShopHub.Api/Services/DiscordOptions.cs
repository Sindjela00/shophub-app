namespace ShopHub.Api.Services;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>The bot's Discord application client id — public, not secret (it's baked into
    /// every invite link a shop owner is handed). Used to build the invite-URL a shop owner
    /// follows to add the bot to their own server.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>The Discord bot's token — this app owns the credential (shop-operator's own
    /// DiscordChannelReconciler reads the same underlying Secret live via the Kubernetes API
    /// instead of holding a copy, precisely so there's exactly one place it's configured).
    /// Injected as a plain env var from the shophub chart's own Secret, the same way
    /// Jwt:SigningKey/Database:ConnectionString already are — no Kubernetes API call needed to
    /// read this app's own credential.</summary>
    public string BotToken { get; set; } = "";
}
