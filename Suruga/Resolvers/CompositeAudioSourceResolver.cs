using Suruga.Primitives;
using Suruga.Resolvers.Abstractions;

namespace Suruga.Resolvers;

internal sealed class CompositeAudioSourceResolver
{
    private readonly IEnumerable<IAudioSourceResolver> _resolvers;

    internal CompositeAudioSourceResolver(IEnumerable<IAudioSourceResolver> resolvers)
        => _resolvers = resolvers;

    internal async Task<Result<AudioSource>> ResolveAsync(string input, CancellationToken token = default)
    {
        Exception? lastError = null;
        
        foreach (IAudioSourceResolver resolver in _resolvers)
        {
            Result<AudioSource> result = await resolver.ResolveAsync(input, token);

            if (result.IsSuccessful)
            {
                return result;
            }

            lastError = result.Error;
        }

        return Result<AudioSource>.Failure(lastError!);
    }
}
