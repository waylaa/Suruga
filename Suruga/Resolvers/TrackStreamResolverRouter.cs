using System.Collections.Frozen;
using Suruga.Primitives;
using Suruga.Resolvers.Sources;

namespace Suruga.Resolvers;

internal sealed class TrackStreamResolverRouter(IEnumerable<ITrackStreamResolver> resolvers)
{
	private readonly FrozenDictionary<TrackPlatform, ITrackStreamResolver> _resolvers
		= resolvers.ToFrozenDictionary(r => r.Platform);
	
	internal ValueTask<StreamSource> ResolveStreamUriAsync(Track descriptor, CancellationToken token = default)
		=> _resolvers[descriptor.Platform].ResolveStreamUriAsync(descriptor, token);
}
