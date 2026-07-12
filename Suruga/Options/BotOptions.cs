namespace Suruga.Options;

/// <summary>
/// Represents configuration options for this application.
/// </summary>
internal sealed record BotOptions
{
    /// <summary>
    /// Gets or sets the bot token.
    /// </summary>
    internal required string Token { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the development guild used for guild-scoped command registration.
    /// </summary>
    internal required ulong? DevelopmentGuildId { get; set; }

    /// <summary>
    /// Gets or sets the folder path to the FFmpeg runtimes.
    /// </summary>
    internal required string? FFmpegPath { get; set; }
}
