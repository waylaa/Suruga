using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Web;

namespace Suruga.Primitives;

/// <summary>
/// Represents a user-provided input that can be either a local audio file
/// or a YouTube resource.
/// </summary>
internal sealed class Input : IParsable<Input>
{
	/// <summary>
	/// Gets the type of input that was identified.
	/// </summary>
	internal InputType Type { get; }

	/// <summary>
	/// Gets the normalized string value of the input.
	/// </summary>
	internal string Value { get; }

	private static readonly string[] SupportedFileExtensions = [".wav", ".flac", ".opus", ".aac", ".ogg", ".mp3", ".m4a", ".mp4", ".mkv", ".webm"];

	private readonly string _value;

	private Input(string? value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		_value = value;

		if (IsLocalFile(value, out InputType type))
		{
			Type = type;
			Value = _value;

			return;
		}
		
		if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
		    IsYoutubeUrl(uri, out type, out string? normalizedValue))
		{
			Type = type;
			Value = normalizedValue;

			return;
		}
		
		throw new InvalidOperationException($"Could not parse the specified input: {_value}");
	}

	internal static Input Parse(string input)
		=> Parse(input, CultureInfo.InvariantCulture);
	
	internal static bool TryParse(string input, [NotNullWhen(true)] out Input? result)
		=> TryParse(input, CultureInfo.InvariantCulture, out result);
	
	public static Input Parse(string s, IFormatProvider? provider) => new(s);

	public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Input result)
	{
		try
		{
			result = new Input(s);
			return true;
		}
		catch
		{
			result = null;
			return false;
		}
	}

	/// <summary>
	/// Determines whether the input URI points to a local file with a supported extension.
	/// </summary>
	/// <param name="filePath">The URI to inspect.</param>
	/// <param name="type">
	/// When this method returns, contains <see cref="InputType.LocalFile"/> if the
	/// check succeeded.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the URI is a local file and the file extension is
	/// supported. Otherwise, <see langword="false"/>.
	/// </returns>
	private bool IsLocalFile(string filePath, out InputType type)
	{
		type = default;

		if (!Path.IsPathFullyQualified(filePath))
		{
			return false;
		}

		string extension = Path.GetExtension(_value);

		if (string.IsNullOrWhiteSpace(extension))
		{
			return false;
		}

		if (!SupportedFileExtensions.Contains(extension))
		{
			return false;
		}

		if (!File.Exists(filePath))
		{
			return false;
		}
		
		type = InputType.LocalFile;
		return true;
	}

	/// <summary>
	/// Determines whether the input URI represents a YouTube video ID, video URL, or playlist.
	/// </summary>
	/// <param name="uri">The URI to inspect.</param>
	/// <param name="type">
	/// When this method returns, contains the specific type of YouTube resource that was identified.
	/// </param>
	/// <param name="normalizedValue">
	/// When this method returns, contains the normalized value of the input
	/// (e.g., the extracted video ID).
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the input was recognized as a YouTube resource.
	/// Otherwise, <see langword="false"/>.
	/// </returns>
	private bool IsYoutubeUrl(Uri uri, out InputType type, [NotNullWhen(true)] out string? normalizedValue)
	{
		// Parse youtube search queries.
		if (_value.StartsWith("yt:", StringComparison.OrdinalIgnoreCase))
		{
			normalizedValue = _value["yt:".Length..];
			type = InputType.YoutubeSearch;
			
			return true;
		}

		if (_value.StartsWith("youtube:", StringComparison.OrdinalIgnoreCase))
		{
			normalizedValue = _value["youtube:".Length..];
			type = InputType.YoutubeSearch;
			
			return true;
		}
		
		// YouTube video IDs are exactly 11 characters long.
		if (_value.Length == 11 && _value.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
		{
			type = InputType.YoutubeVideoId;
			normalizedValue = _value;
			
			return true;
		}

		string host = uri.Host.ToLowerInvariant();
		
		bool isYoutubeHost =
			host == "youtube.com" ||
			host.EndsWith(".youtube.com") ||
			host == "youtu.be" ||
			host == "youtube-nocookie.com" ||
			host.EndsWith(".youtube-nocookie.com");
		
		if (!isYoutubeHost)
		{
			type = default;
			normalizedValue = null;
			
			return false;
		}
		
		NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
		string? id = query["v"];
		string? playlistId = query["list"];

		// Check if this YouTube URL is a playlist URL.
		if (!string.IsNullOrEmpty(playlistId))
		{
			type = InputType.YoutubePlaylist;
			normalizedValue = _value;
			
			return true;
		}
		
		// If a YouTube URL does not have a "v" query parameter, get its ID from
		// its URL segments. Some examples include:
		// https://youtu.be/VIDEO_ID,
		// https://www.youtube.com/shorts/VIDEO_ID,
		// https://www.youtube.com/embed/VIDEO_ID
		// https://www.youtube-nocookie.com/embed/VIDEO_ID
		if (string.IsNullOrEmpty(id))
		{
			type = InputType.YoutubeVideoId;

			if (uri.Segments.Length < 1)
			{
				type = default;
				normalizedValue = null;
				
				return false;
			}
			
			normalizedValue = uri.Segments[1]; // Get the video ID.
			return true;
		}

		type = InputType.YoutubeVideoUrl;
		normalizedValue = _value;
		
		return true;
	}
}
