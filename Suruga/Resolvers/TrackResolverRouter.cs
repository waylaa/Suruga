using System.Collections.Frozen;
using Suruga.Primitives;
using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers;

/// <summary>
/// Routes track resolution requests through a collection of <see cref="ITrackResolver"/> instances.
/// </summary>
/// <remarks>
/// The router attempts resolution using each registered resolver in order, returning
/// the first non-empty result.
/// </remarks>
internal sealed class TrackResolverRouter(IEnumerable<ITrackResolver> resolvers)
{
    private readonly FrozenDictionary<TrackPlatform, ITrackResolver> _resolvers
        = resolvers.ToFrozenDictionary(r => r.Platform);
    
    /// <summary>
    /// Resolves the given input by delegating to each registered resolver in sequence.
    /// </summary>
    /// <param name="input">The input string to resolve (e.g., URL, file path, or search query).</param>
    /// <param name="context"></param>
    /// <param name="token">A token that can cancel the resolution operation.</param>
    /// <returns>
    /// A task that completes with a read-only list of resolved <see cref="Track"/> instances.
    /// If no resolver produces any tracks, an empty list is returned.
    /// </returns>
    internal async Task<Result<TrackSet>> ResolveAsync(string input, TrackRequestContext context, CancellationToken token = default)
    {
        if (!Input.TryParse(input, out Input? result))
        {
            return TrackSet.Empty;
        }

        return result.Type switch
        {
            InputType.LocalFile => await _resolvers[TrackPlatform.Local].ResolveAsync(result, context, token),
            
            InputType.YoutubeVideoId or InputType.YoutubeVideoUrl or InputType.YoutubePlaylist or InputType.YoutubeSearch =>
                await _resolvers[TrackPlatform.Youtube].ResolveAsync(result, context, token),
            
            _ => TrackSet.Empty
        };
    }
}
