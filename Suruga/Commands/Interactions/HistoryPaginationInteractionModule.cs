using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Helpers;
using Suruga.Pagination;
using Suruga.Primitives;

namespace Suruga.Commands.Interactions;

internal sealed class HistoryPaginationInteractionModule
(
	AudioSessionManager sessionManager,
	PaginatorManager paginatorManager
) : ComponentInteractionModule<ComponentInteractionContext>
{
	[ComponentInteraction("history_page_previous")]
	public async Task ShowPreviousPage()
	{
		if (!sessionManager.SessionExists(Context.Guild!.Id) || await GetPaginatorStateAsync() is not PaginatorSession<Track> state)
		{
			return;
		}
		
		state.Paginator.MoveToPreviousPage();
		await ModifyPaginatedMessageAsync(state.Paginator);
	}
	
	[ComponentInteraction("history_page_next")]
	public async Task ShowNextPage()
	{
		if (!sessionManager.SessionExists(Context.Guild!.Id) || await GetPaginatorStateAsync() is not PaginatorSession<Track> state)
		{
			return;
		}
		
		state.Paginator.MoveToNextPage();
		await ModifyPaginatedMessageAsync(state.Paginator);
	}

	private async Task<PaginatorSession<Track>?> GetPaginatorStateAsync()
	{
		return paginatorManager.TryGet(Context.Guild!.Id, out PaginatorSession<Track>? state) ? state : null;
	}

	private async Task ModifyPaginatedMessageAsync(Paginator<Track> paginator)
	{
		EmbedProperties embed = EmbedHelper.History((GuildUser)Context.User, paginator);
		
		await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
			.WithEmbeds([embed])
			.WithComponents([ComponentsHelper.CreateHistoryPaginationButtonsComponent(paginator)])));
	}
}
