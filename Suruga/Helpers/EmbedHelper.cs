using System.Text;
using Microsoft.Extensions.ObjectPool;
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
	private static readonly ObjectPool<StringBuilder> StringBuilderPool =
		new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy());
	
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

    /// <summary>
    /// Creates an embed representing the current queue state.
    /// </summary>
    /// <param name="currentTrack">The currently playing track, if any.</param>
    /// <param name="user">The user requesting the queue.</param>
    /// <param name="paginator">The paginated queue data.</param>
    /// <returns>An embed containing the queue contents.</returns>
    internal static EmbedProperties Queue(Track? currentTrack, GuildUser user, Paginator<Track> paginator)
	{
		StringBuilder builder = StringBuilderPool.Get();
		
		if (currentTrack is not null)
		{
			builder.AppendLine($"▶ Now playing: {currentTrack}");
		}

		foreach ((int Index, Track Track) value in paginator.GetPage().Index())
		{
			builder.AppendLine($"{value.Index}. {value.Track}");
		}

		if (builder.Length <= 0)
		{
			builder.AppendLine("Queue is empty.");
		}
		
		string description = builder.ToString();
		StringBuilderPool.Return(builder);

		return new EmbedProperties()
			.WithColor(SuccessColor)
			.WithTimestamp(DateTimeOffset.Now)
			.WithTitle("Queue")
			.WithDescription(description)
			.WithFooter(new EmbedFooterProperties()
				.WithIconUrl(GetUserAvatarUrl(user))
				.WithText($"{GetUserName(user)} • Page {paginator.CurrentPage + 1}/{paginator.TotalPages}"));
	}

    /// <summary>
    /// Creates an embed representing playback history.
    /// </summary>
    /// <param name="user">The user requesting the history.</param>
    /// <param name="paginator">The paginated history data.</param>
    /// <returns>An embed containing playback history.</returns>
    internal static EmbedProperties History(GuildUser user, Paginator<Track> paginator)
	{
		StringBuilder builder = StringBuilderPool.Get();

		foreach ((int Index, Track Track) value in paginator.GetPage().Index())
		{
			builder.AppendLine($"{value.Index}. {value.Track}");
		}

		if (builder.Length <= 0)
		{
			builder.AppendLine("No tracks have been played.");
		}
		
		string description = builder.ToString();
		StringBuilderPool.Return(builder);
		
		return new EmbedProperties()
			.WithColor(SuccessColor)
			.WithTimestamp(DateTimeOffset.Now)
			.WithTitle("Playback History")
			.WithDescription(description)
			.WithFooter(new EmbedFooterProperties()
				.WithIconUrl(GetUserAvatarUrl(user))
				.WithText($"{GetUserName(user)} • Page {paginator.CurrentPage + 1}/{paginator.TotalPages}"));
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

    /// <summary>
    /// Gets the best available avatar URL for a guild user.
    /// </summary>
    /// <param name="user">The user whose avatar should be retrieved.</param>
    /// <returns>A string representation of the user's avatar URL.</returns>
    private static string GetUserAvatarUrl(GuildUser user)
		=> user.GetGuildAvatarUrl()?.ToString() ?? user.GetAvatarUrl()?.ToString() ?? user.DefaultAvatarUrl.ToString();

    /// <summary>
    /// Gets the display name for a guild user.
    /// </summary>
    /// <param name="user">The user whose name should be retrieved.</param>
    /// <returns>The best available display name.</returns>
    private static string GetUserName(GuildUser user)
		=> user.Nickname ?? user.GlobalName ?? user.Username;
}
