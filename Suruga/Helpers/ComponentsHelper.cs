using NetCord;
using NetCord.Rest;
using Suruga.Pagination;

namespace Suruga.Helpers;

internal static class ComponentsHelper
{
    internal static ActionRowProperties CreatePlayerControlsComponent(bool isPaused)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("player_loop_toggle", "↻", ButtonStyle.Secondary),
			new ButtonProperties("player_rewind", "⏮", ButtonStyle.Secondary),
			new ButtonProperties(isPaused ? "player_resume" : "player_pause", isPaused ? "▶" : "⏸", isPaused ? ButtonStyle.Success : ButtonStyle.Primary),
			new ButtonProperties("player_skip", "⏭", ButtonStyle.Secondary),
			new ButtonProperties("player_stop", "⏹", ButtonStyle.Danger)
		);
	}
    
    internal static ActionRowProperties CreateQueuePaginationButtonsComponent<T>(Paginator<T> paginator)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("queue_page_previous", "←", ButtonStyle.Secondary) { Disabled = paginator.IsAtFirstPage },
			new ButtonProperties("queue_page_next", "→", ButtonStyle.Secondary) { Disabled = paginator.IsAtLastPage }
		);
	}
    
    internal static ActionRowProperties CreateHistoryPaginationButtonsComponent<T>(Paginator<T> paginator)
	{
		return new ActionRowProperties().AddComponents
		(
			new ButtonProperties("history_page_previous", "←", ButtonStyle.Secondary) { Disabled = paginator.IsAtFirstPage },
			new ButtonProperties("history_page_next", "→", ButtonStyle.Secondary) { Disabled = paginator.IsAtLastPage }
		);
	}
}
