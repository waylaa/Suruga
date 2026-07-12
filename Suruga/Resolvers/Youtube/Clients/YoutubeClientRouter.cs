using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients;

/// <summary>
/// Routes requests through a collection of YouTube clients, falling back to the next client on failure.
/// </summary>
/// <param name="clients">The registered Innertube clients.</param>
internal sealed class YoutubeClientRouter(IEnumerable<YoutubeClient> clients)
{
	private readonly YoutubeClient[] _clients = clients.ToArray();
	
	/// <summary>
	/// Executes a request against each registered Innertube client until one succeeds.
	/// </summary>
	/// <param name="action">
	/// A delegate that performs the request using a specific
	/// <see cref="YoutubeClient"/> instance.
	/// </param>
	/// <param name="token">A token that can cancel the operation.</param>
	/// <typeparam name="T">The type of the result returned by the request.</typeparam>
	/// <returns>
	/// A task that completes with the result of the first successful
	/// client, or <see langword="null"/> if all clients fail.
	/// </returns>
	internal async Task<Result<T>> RequestAsync<T>
	(
		Func<YoutubeClient, CancellationToken, Task<Result<T>>> action,
		CancellationToken token = default
	) where T : class
	{
		Exception? lastException = null;
		
		foreach (YoutubeClient client in _clients)
		{
			Result<T> result = await action(client, token);

			if (result.IsSuccessful)
			{
				return result;
			}
			
			lastException = result.Error;
		}

		return Result<T>.Failure(lastException ?? throw new Exception("An unknown error occured."));
	}
}
