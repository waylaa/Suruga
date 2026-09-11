using NetCord;
using NetCord.Rest;

namespace Suruga.Helpers;

internal static class DiscordUserDisplay
{
    internal static EmbedFooterProperties PaginationFooter(GuildUser user, int currentPage, int totalPages)
        => new EmbedFooterProperties()
            .WithIconUrl(AvatarUrl(user))
            .WithText($"{Name(user)} • Page {currentPage + 1}/{totalPages}");
    
    private static string AvatarUrl(GuildUser user)
        => user.GetGuildAvatarUrl()?.ToString() ?? user.GetAvatarUrl()?.ToString() ?? user.DefaultAvatarUrl.ToString();

    private static string Name(GuildUser user)
        => user.Nickname ?? user.GlobalName ?? user.Username;
}
