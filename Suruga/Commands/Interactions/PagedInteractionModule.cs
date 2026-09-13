using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Suruga.Audio;
using Suruga.Helpers;
using Suruga.Pagination;
using Suruga.Primitives;

namespace Suruga.Commands.Interactions;

internal abstract class PaginatedInteractionModule(AudioSessionManager sessionManager, PaginatorManager paginatorManager) : ComponentInteractionModule<ComponentInteractionContext>
{
    protected async Task HandlePageMoveAsync(Action<Paginator<Track>> movePage, Func<AudioSession, Paginator<Track>, EmbedProperties> buildEmbed)
    {
        if (!sessionManager.TryGetSession(Context.Guild!.Id, out AudioSession? session) ||
            !paginatorManager.TryGet(Context.Guild!.Id, out PaginatorSession<Track>? state))
        {
            return;
        }

        movePage(state.Paginator);
        EmbedProperties embed = buildEmbed(session, state.Paginator);

        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithEmbeds([embed])
            .WithComponents([ComponentsHelper.CreateHistoryPaginationButtonsComponent(state.Paginator)])));
    }
}
