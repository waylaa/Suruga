using System.Collections.Specialized;
using System.Text.Json.Serialization;
using System.Web;

namespace Suruga.Resolvers.Primitives;

/// <summary>
/// Represents an adaptive audio format available from a streaming source.
/// </summary>
public sealed class AdaptiveFormat
{
	/// <summary>
	/// Gets the URI of the audio stream.
	/// </summary>
	[JsonInclude]
	internal string StreamUri { get; }
	
	/// <summary>
	/// Gets the codec used for the audio format.
	/// </summary>
	[JsonInclude]
	internal string Codec { get; }
	
	/// <summary>
	/// Gets the content length of the stream in bytes, if available.
	/// </summary>
	[JsonInclude]
	internal long? ContentLength { get; }

	/// <summary>
	/// Gets the remaining time until the stream URI expires.
	/// </summary>
	[JsonInclude]
	internal TimeSpan Expiration { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AdaptiveFormat"/> record.
	/// </summary>
	/// <param name="streamUri">The URI of the audio stream.</param>
	/// <param name="codec">The codec used for the audio format.</param>
	/// <param name="contentLength">The content length of the stream, or <see langword="null"/> if unknown.</param>
	internal AdaptiveFormat(string streamUri, string codec, long? contentLength)
	{
		StreamUri = streamUri;
		Codec = codec;
		ContentLength = contentLength;
		Expiration = GetExpiration(streamUri);
	}

	/// <summary>
	/// Extracts the expiration time from the query parameter of the stream URI.
	/// </summary>
	/// <param name="streamUrl">The stream URI containing an 'expiry' query parameter.</param>
	/// <returns>The remaining time until the stream URI expires.</returns>
	private static TimeSpan GetExpiration(string streamUrl)
	{
		NameValueCollection queries = HttpUtility.ParseQueryString(new Uri(streamUrl).Query);
		
		string expiry = queries["expire"]!;
		long expiryInUnixTimestamp = long.Parse(expiry);
		
		return DateTimeOffset.FromUnixTimeSeconds(expiryInUnixTimestamp) - DateTimeOffset.UtcNow;
	}
}
