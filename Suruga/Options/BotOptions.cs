namespace Suruga.Options;

internal sealed record BotOptions
{
    internal required string Token { get; set; }
    
    internal required ulong? DevelopmentGuildId { get; set; }
    
    internal required string FFmpegPath { get; set; }
}
