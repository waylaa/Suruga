using Suruga.Primitives;

namespace Suruga.Resolvers;

internal interface ITrackStreamResolver
{
	TrackPlatform Platform { get; }
	
	ValueTask<StreamSource> ResolveStreamUriAsync(Track descriptor, CancellationToken token = default);
}
