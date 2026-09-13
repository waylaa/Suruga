namespace Suruga.Pagination;

/// <summary>
/// Represents the state of an active paginator session.
/// </summary>
/// <typeparam name="T">The item type being paginated.</typeparam>
internal sealed class PaginatorSession<T> : IPaginatorSession
{
    /// <summary>
    /// Gets a token that is canceled when the session expires or is disposed.
    /// </summary>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>
    /// Gets the paginator associated with this session.
    /// </summary>
    internal Paginator<T> Paginator { get; }

	private readonly CancellationTokenSource _cancellation = new();
	
    internal PaginatorSession(Paginator<T> paginator)
		=> Paginator = paginator;
    
    public async ValueTask DisposeAsync()
	{
		if (!_cancellation.IsCancellationRequested)
		{
			await _cancellation.CancelAsync();
		}
		
		_cancellation.Dispose();
	}
}
