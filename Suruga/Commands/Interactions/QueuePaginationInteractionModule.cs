using NetCord;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Helpers;
using Suruga.Pagination;

namespace Suruga.Commands.Interactions;

internal sealed class QueuePaginationInteractionModule(AudioSessionManager sessionManager, PaginatorManager paginatorManager) : PaginatedInteractionModule(sessionManager, paginatorManager)
{
	[ComponentInteraction("queue_page_previous")]
	public Task ShowPreviousPage()
		=> HandlePageMoveAsync(p => p.MoveToPreviousPage(), (s, p) => EmbedHelper.Queue(s.Player.Queue.CurrentTrack, (GuildUser)Context.User, p));

	[ComponentInteraction("queue_page_next")]
	public Task ShowNextPage()
		=> HandlePageMoveAsync(p => p.MoveToNextPage(), (s, p) => EmbedHelper.Queue(s.Player.Queue.CurrentTrack, (GuildUser)Context.User, p));
}
