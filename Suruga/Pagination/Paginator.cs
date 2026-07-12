using System.Collections.ObjectModel;

namespace Suruga.Pagination;

/// <summary>
/// Provides page-based navigation over a collection of items.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
internal sealed class Paginator<T>
{
    /// <summary>
    /// Gets the total number of available pages.
    /// </summary>
    internal int TotalPages => Math.Max(1, (int)Math.Ceiling(_items.Count / (double)_pageSize));

    /// <summary>
    /// Gets whether the current page is the first page.
    /// </summary>
    internal bool IsAtFirstPage => CurrentPage == 0;

    /// <summary>
    /// Gets whether the current page is the last page.
    /// </summary>
    internal bool IsAtLastPage => CurrentPage >= TotalPages - 1;

    /// <summary>
    /// Gets the current page index.
    /// </summary>
    internal int CurrentPage { get; private set; }
	
	private readonly IReadOnlyList<T> _items;
	private readonly int _pageSize;

    /// <summary>
    /// Initializes a new paginator instance.
    /// </summary>
    /// <param name="items">The items to paginate.</param>
    /// <param name="pageSize">The number of items per page.</param>
    internal Paginator(IReadOnlyList<T> items, int pageSize)
	{
		_items = items;
		_pageSize = pageSize;
	}

    /// <summary>
    /// Gets the items on the current page.
    /// </summary>
    /// <returns>A read-only collection containing the page items.</returns>
    internal IReadOnlyList<T> GetPage()
	{
		if (_items.Count == 0)
		{
            return [];
		}

		int start = CurrentPage * _pageSize;
		int count = Math.Min(_pageSize, _items.Count - start);
		int end = Math.Min(start + count, _items.Count);
		
		if (start < 0 || start >= _items.Count)
		{
            return [];
		}

		return _items
			.Take(new Range(start, end))
			.ToList()
			.AsReadOnly();
	}

    /// <summary>
    /// Moves to the next page if one exists.
    /// </summary>
    internal void MoveToNextPage()
	{
		if (CurrentPage < TotalPages - 1)
		{
			CurrentPage++;
		}
	}

    /// <summary>
    /// Moves to the previous page if one exists.
    /// </summary>
    internal void MoveToPreviousPage()
	{
		if (CurrentPage > 0)
		{
			CurrentPage--;
		}
	}
}
