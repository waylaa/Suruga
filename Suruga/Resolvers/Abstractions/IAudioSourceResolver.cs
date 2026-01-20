using Suruga.Primitives;

namespace Suruga.Resolvers.Abstractions;

internal interface IAudioSourceResolver
{
    Task<Result<AudioSource>> ResolveAsync(string input, CancellationToken token = default);
}
