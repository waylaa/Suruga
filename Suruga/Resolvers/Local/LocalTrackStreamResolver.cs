using Suruga.Primitives;
using Suruga.Resolvers.Sources;

namespace Suruga.Resolvers.Local;

internal sealed class LocalTrackStreamResolver : ITrackStreamResolver
{
	public TrackPlatform Platform => TrackPlatform.Local;
	
	public ValueTask<StreamSource> ResolveStreamUriAsync(Track descriptor, CancellationToken token = default)
	{
		return !string.IsNullOrWhiteSpace(descriptor.Uri)
			? ValueTask.FromResult<StreamSource>(new LocalStreamSource(descriptor.Uri))
			: ValueTask.FromException<StreamSource>(new InvalidOperationException("Local track does not have a URI."));
	}
}
