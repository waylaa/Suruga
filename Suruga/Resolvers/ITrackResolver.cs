using Suruga.Primitives;
using Suruga.Resolvers.Primitives;

namespace Suruga.Resolvers;

/// <summary>
/// Defines a mechanism for resolving audio tracks from a given <see cref="Input"/>.
/// </summary>
internal interface ITrackResolver
{
    TrackPlatform Platform { get; }
    
    /// <summary>
    /// Asynchronously resolves the specified input into a collection of tracks (or a
    /// single track inside a collection).
    /// </summary>
    /// <param name="input">The input string to resolve (e.g, URL, file path, or search query).</param>
    /// <param name="requestedBy"></param>
    /// <param name="token">A token that can cancel the resolution operation.</param>
    /// <returns>
    /// A task that completes with a read-only list of resolved <see cref="Track"/> instances.
    /// </returns>
    ValueTask<Result<TrackSet>> ResolveAsync(Input input, TrackRequestContext requestedBy, CancellationToken token = default);
}
