using System.Text;
using Suruga.Pagination;

namespace Suruga.Helpers;

internal static class PaginatedListRenderer
{
    private static readonly StringBuilder StringBuilder = new();

    internal static string Render<T>(Paginator<T> paginator, string emptyMessage, string? leadingLine = null)
    {
        try
        {
            if (leadingLine is not null)
            {
                StringBuilder.AppendLine(leadingLine);
            }

            foreach ((int Index, T Item) value in paginator.GetPage().Index())
            {
                StringBuilder.AppendLine($"{value.Index}. {value.Item}");
            }

            return StringBuilder.Length > 0 ? StringBuilder.ToString() : emptyMessage;
        }
        finally
        {
            StringBuilder.Clear();
        }
    }
}
