using NetCord;
using NetCord.Rest;
using Suruga.Pagination;

namespace Suruga.Helpers;

/// <summary>
/// Provides helper methods for creating Discord message component action rows.
/// </summary>
/// <remarks>
/// This class centralizes creation of reusable button layouts for player controls,
/// queue pagination, and history pagination.
/// </remarks>
internal static class ComponentsHelper
{
    /// <summary>
    /// Creates an action row containing player control buttons.
    /// </summary>
    /// <param name="isPaused">
    /// Indicates whether playback is currently paused.
    /// When <see langword="true"/>, the play button will be shown as a resume button (▶).
    /// When <see langword="false"/>, the button will be shown as a pause button (⏸).
    /// </param>
    /// <returns>
    /// An <see cref="ActionRowProperties"/> containing player control buttons:
    /// <list type="bullet">
    /// <item>↻ Loop toggle</item>
    /// <item>⏮ Previous track</item>
    /// <item>▶/⏸ Play or pause toggle</item>
    /// <item>⏭ Skip track</item>
    /// <item>⏹ Stop playback</item>
    /// </list>
    /// </returns>
    internal static ActionRowProperties CreatePlayerControlsComponent(bool isPaused)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("player_loop_toggle", "↻", ButtonStyle.Secondary),
			new ButtonProperties("player_back", "⏮", ButtonStyle.Secondary),
			new ButtonProperties(isPaused ? "player_resume" : "player_pause", isPaused ? "▶" : "⏸", isPaused ? ButtonStyle.Success : ButtonStyle.Primary),
			new ButtonProperties("player_skip", "⏭", ButtonStyle.Secondary),
			new ButtonProperties("player_stop", "⏹", ButtonStyle.Danger)
		);
	}

    /// <summary>
    /// Creates pagination buttons for queue navigation.
    /// </summary>
    /// <typeparam name="T">The type of items being paginated.</typeparam>
    /// <param name="paginator">The paginator controlling queue pages.</param>
    /// <returns>An action row containing previous/next navigation buttons.</returns>
    internal static ActionRowProperties CreateQueuePaginationButtonsComponent<T>(Paginator<T> paginator)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("queue_page_previous", "←", ButtonStyle.Secondary) { Disabled = paginator.IsAtFirstPage },
			new ButtonProperties("queue_page_next", "→", ButtonStyle.Secondary) { Disabled = paginator.IsAtLastPage }
		);
	}

    /// <summary>
    /// Creates pagination buttons for history navigation.
    /// </summary>
    /// <typeparam name="T">The type of items being paginated.</typeparam>
    /// <param name="paginator">The paginator controlling history pages.</param>
    /// <returns>An action row containing previous/next navigation buttons.</returns>
    internal static ActionRowProperties CreateHistoryPaginationButtonsComponent<T>(Paginator<T> paginator)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("history_page_previous", "←", ButtonStyle.Secondary) { Disabled = paginator.IsAtFirstPage },
			new ButtonProperties("history_page_next", "→", ButtonStyle.Secondary) { Disabled = paginator.IsAtLastPage }
		);
	}
}
