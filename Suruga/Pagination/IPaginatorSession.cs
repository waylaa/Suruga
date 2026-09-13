namespace Suruga.Pagination;

internal interface IPaginatorSession : IAsyncDisposable
{
    CancellationToken CancellationToken { get; }
}
