using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Helpers;
using Suruga.Pagination;
using Suruga.Primitives;

namespace Suruga.Commands.Interactions;

internal sealed class QueuePaginationInteractionModule
(
	AudioSessionManager sessionManager,
	PaginatorManager paginatorManager
) : ComponentInteractionModule<ComponentInteractionContext>
{
	[ComponentInteraction("queue_page_previous")]
	public async Task ShowPreviousPage()
	{
		if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session) ||
		    !paginatorManager.TryGet(Context.Guild!.Id, out PaginatorSession<Track>? state))
		{
			return;
		}
		
		state.Paginator.MoveToPreviousPage();
		await ModifyPaginatedMessageAsync(session.Player.Queue.CurrentTrack, state.Paginator);
	}
	
	[ComponentInteraction("queue_page_next")]
	public async Task ShowNextPage()
	{
		if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session) ||
		    !paginatorManager.TryGet(Context.Guild!.Id, out PaginatorSession<Track>? state))
		{
			return;
		}
		
		state.Paginator.MoveToNextPage();
		await ModifyPaginatedMessageAsync(session.Player.Queue.CurrentTrack, state.Paginator);
	}

	private async Task ModifyPaginatedMessageAsync(Track? currentTrack, Paginator<Track> paginator)
	{
		EmbedProperties embed = EmbedHelper.Queue(currentTrack, (GuildUser)Context.User, paginator);
		
		await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
			.WithEmbeds([embed])
			.WithComponents([ComponentsHelper.CreateHistoryPaginationButtonsComponent(paginator)])));
	}
}
