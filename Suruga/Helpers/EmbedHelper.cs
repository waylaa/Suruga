using NetCord;
using NetCord.Rest;
using Suruga.Pagination;
using Suruga.Primitives;

namespace Suruga.Helpers;

/// <summary>
/// Provides helper methods for creating Discord embed responses used by the bot.
/// </summary>
/// <remarks>
/// This class centralizes embed creation for common music playback scenarios such as
/// now playing, queue display, history, and status notifications.
/// </remarks>
internal static class EmbedHelper
{
	private static readonly Color SuccessColor = new(215, 0, 64);

	private static readonly Color FailureColor = new(139, 0, 0);

	private static readonly Color WarningColor = new(233, 213, 2);

    /// <summary>
    /// Creates an embed indicating the currently playing track.
    /// </summary>
    /// <param name="track">The track that is currently playing.</param>
    /// <returns>An embed describing the now playing track.</returns>
    internal static EmbedProperties NowPlaying(Track track)
		=> BuildTrackEmbed(track, "▶️ Now playing", SuccessColor);

    /// <summary>
    /// Creates an embed indicating playback has been paused.
    /// </summary>
    /// <param name="track">The track that is currently paused.</param>
    /// <returns>An embed describing the paused track.</returns>
    internal static EmbedProperties Paused(Track track)
		=> BuildTrackEmbed(track, "⏸️ Paused", SuccessColor);

    /// <summary>
    /// Creates an embed indicating playback failure.
    /// </summary>
    /// <param name="track">The track that failed to play.</param>
    /// <returns>An embed describing the failed track.</returns>
    internal static EmbedProperties Errored(Track track)
		=> BuildTrackEmbed(track, "❌ Failed to play", FailureColor);

    internal static EmbedProperties Queue(Track? currentTrack, GuildUser user, Paginator<Track> paginator)
    {
	    string leadingLine = currentTrack is not null ? $"▶ Now playing: {currentTrack}" : null!;
	    string description = PaginatedListRenderer.Render(paginator, "Queue is empty.", leadingLine);

	    return new EmbedProperties()
		    .WithColor(SuccessColor)
		    .WithTimestamp(DateTimeOffset.Now)
		    .WithTitle("Queue")
		    .WithDescription(description)
		    .WithFooter(DiscordUserDisplay.PaginationFooter(user, paginator.CurrentPage, paginator.TotalPages));
    }

    internal static EmbedProperties History(GuildUser user, Paginator<Track> paginator)
    {
	    string description = PaginatedListRenderer.Render(paginator, "No tracks have been played.");

	    return new EmbedProperties()
		    .WithColor(SuccessColor)
		    .WithTimestamp(DateTimeOffset.Now)
		    .WithTitle("Playback History")
		    .WithDescription(description)
		    .WithFooter(DiscordUserDisplay.PaginationFooter(user, paginator.CurrentPage, paginator.TotalPages));
    }

    /// <summary>
    /// Creates an embed indicating that playback was automatically paused.
    /// </summary>
    /// <returns>An embed describing the auto-pause event.</returns>
    internal static EmbedProperties AutoPause()
	{
		return new EmbedProperties()
			.WithTitle("Audio paused")
			.WithDescription("Playback automatically paused due to server mute.")
			.WithColor(WarningColor);
	}

	private static EmbedProperties BuildTrackEmbed(Track track, string title, Color color)
	{
		return new EmbedProperties()
			.WithThumbnail(track.ThumbnailUri)
			.WithColor(color)
			.WithTimestamp(DateTimeOffset.Now)
			.AddFields
			(
				new EmbedFieldProperties()
					.WithName(title)
					.WithValue(track.ToString()),
				new EmbedFieldProperties()
					.WithName("Author")
					.WithValue(track.Author)
					.WithInline(),
				new EmbedFieldProperties()
					.WithName("Duration")
					.WithValue($@"{track.Duration:hh\:mm\:ss}")
					.WithInline()
			)
			.WithFooter(new EmbedFooterProperties()
				.WithIconUrl(track.RequestedBy?.AvatarUrl)
				.WithText(track.RequestedBy?.Name));
	}
}
