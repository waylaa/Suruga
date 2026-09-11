using NetCord;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Helpers;
using Suruga.Pagination;

namespace Suruga.Commands.Interactions;

internal sealed class HistoryPaginationInteractionModule(AudioSessionManager sessionManager, PaginatorManager paginatorManager)
	: PaginatedInteractionModule(sessionManager, paginatorManager)
{
	[ComponentInteraction("history_page_previous")]
	public Task ShowPreviousPage()
		=> HandlePageMoveAsync(paginator => paginator.MoveToPreviousPage(), (_, p) => EmbedHelper.History((GuildUser)Context.User, p));

	[ComponentInteraction("history_page_next")]
	public Task ShowNextPage()
		=> HandlePageMoveAsync(paginator => paginator.MoveToNextPage(), (_, p) => EmbedHelper.History((GuildUser)Context.User, p));
}
