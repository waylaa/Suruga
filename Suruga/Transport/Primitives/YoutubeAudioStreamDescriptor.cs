using Suruga.Primitives;
using Suruga.Resolvers.Abstractions;
using Suruga.Resolvers.Youtube.Clients.Abstractions;

namespace Suruga.Transport.Primitives;

internal sealed record YoutubeAudioStreamDescriptor
(
    string Url,
    TimeSpan Expiration,
    Func<Task<Result<AudioSource>>> ResolveDelegate,
    YoutubeClientBase BoundClient
) : AudioStreamDescriptorBase(Url);