namespace Suruga.Pagination;

/// <summary>
/// Represents a disposable paginator session.
/// </summary>
internal interface IPaginatorSession : IAsyncDisposable
{
    CancellationToken CancellationToken { get; }
}
