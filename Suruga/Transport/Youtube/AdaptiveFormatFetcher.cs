using System.Net;
using System.Net.Http.Headers;

namespace Suruga.Transport.Youtube;

internal sealed class AdaptiveFormatFetcher(HttpClient client)
{
    internal int FetchRange(long chunkStart, long chunkEnd, Memory<byte> buffer, string streamUri)
    {
        int expectedBytes = (int)(chunkEnd - chunkStart + 1);

        using HttpRequestMessage request = new(HttpMethod.Get, streamUri);
        request.Headers.Range = new RangeHeaderValue(chunkStart, chunkEnd);

        using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using Stream networkStream = response.Content.ReadAsStream();
        networkStream.ReadExactly(buffer.Span[..expectedBytes]);

        return expectedBytes;
    }

    internal long GetContentLength(string streamUri)
    {
        using HttpRequestMessage request = new(HttpMethod.Head, streamUri);
        using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentRange?.Length is long totalBytes)
        {
            return totalBytes;
        }

        if (response.Content.Headers.ContentLength is long contentLength && response.StatusCode is HttpStatusCode.OK)
        {
            return contentLength;
        }

        throw new InvalidOperationException("Could not determine content length for this audio byte stream.");
    }
}
