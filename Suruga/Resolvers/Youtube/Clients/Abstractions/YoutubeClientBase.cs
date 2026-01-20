using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Suruga.Caching;
using Suruga.Primitives;

namespace Suruga.Resolvers.Youtube.Clients.Abstractions;

internal abstract class YoutubeClientBase(IHttpClientFactory clientFactory, IMemoryCache cache)
{
    private readonly HttpClient _youtubeClient = clientFactory.CreateClient("client");
    private readonly HttpClient _apkMirrorClient = clientFactory.CreateClient("apkmirror");
    private readonly ExpirableCache<string> _cache = new(cache);

    internal abstract Task<Result<JsonNode>> GetPlayerAsync(string videoUrl, CancellationToken token = default);

    internal abstract Task<Result<JsonNode>> SearchAsync(string query, CancellationToken token = default);
    
    internal async Task<Result<HttpResponseMessage>> GetResponseAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        string clientVersion = this switch
        {
            YoutubeAndroidClient or YoutubeEmbeddedAndroidClient => await GetClientVersionAsync
            (
                "youtube",
                "youtube_android_client_version", 
                "20.51.39",
                token
            ),
            YoutubeMusicAndroidClient => await GetClientVersionAsync
            (
                "youtube-music",
                "youtube_music_android_client_version",
                "8.50.51",
                token
            ),
            _ => throw new InvalidOleVariantTypeException("Invalid youtube client.")
        };

        string userAgent = this switch
        {
            YoutubeAndroidClient or YoutubeEmbeddedAndroidClient => $"com.google.android.youtube/{clientVersion} (Linux; U; Android 11; GB) gzip",
            YoutubeMusicAndroidClient => $"com.google.android.apps.youtube.music/{clientVersion} (Linux; U; Android 11; GB) gzip",
            _ => throw new InvalidOleVariantTypeException("Invalid youtube client.")
        };

        string originOrReferrer = this switch
        {
            YoutubeAndroidClient or YoutubeEmbeddedAndroidClient => "https://www.youtube.com",
            YoutubeMusicAndroidClient => "https://music.youtube.com",
            _ => throw new InvalidOleVariantTypeException("Invalid youtube client.")
        };
        
        request.Headers.UserAgent.ParseAdd(userAgent);
        request.Headers.Referrer = new Uri($"{originOrReferrer}/");
        request.Headers.Add("Origin", originOrReferrer);
        // request.Headers.Accept.ParseAdd("*/*");
        // request.Headers.AcceptEncoding.Clear(); // Force identity.
        // request.Headers.AcceptEncoding.ParseAdd("identity");
        
        HttpResponseMessage response = await _youtubeClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); // HANG/DEADLOCK HERE.
        
        return response.IsSuccessStatusCode
            ? Result<HttpResponseMessage>.Success(response)
            : Result<HttpResponseMessage>.Failure($"Unsuccessful HTTP response: {response.StatusCode}");
    }

    protected async Task<string> GetClientVersionAsync(string packageName, string cacheKey, string fallbackVersion, CancellationToken token = default)
    {
        if (_cache.TryGet(cacheKey, out string? cachedVersion))
        {
            return cachedVersion;
        }
        
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"https://www.apkmirror.com/apk/google-inc/{packageName}/");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:146.0) Gecko/20100101 Firefox/146.0");
            
            using HttpResponseMessage response = await _apkMirrorClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync(token);
            Match match = Regex.Match(html, @"YouTube\s+(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
            string version = match.Success ? match.Groups[1].Value : fallbackVersion; // Fallback to current known good version.

            _cache.Set(cacheKey, version, TimeSpan.FromDays(1));
            return version;
        }
        catch
        {
            return fallbackVersion; // On any scrape failure, use a known good fallback.
        }
    }
    
    // Source: https://github.com/Tyrrrz/YoutubeExplode/blob/master/YoutubeExplode/Videos/VideoController.cs
    protected async Task<string?> GetVisitorDataAsync(string userAgent, CancellationToken token = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "https://www.youtube.com/sw.js_data");
        request.Headers.UserAgent.ParseAdd(userAgent);
        request.Headers.Accept.ParseAdd("application/json");

        Result<HttpResponseMessage> responseResult = await GetResponseAsync(request, token);

        if (!responseResult.TryGetValue(out HttpResponseMessage? response) || !response.IsSuccessStatusCode)
        {
            return null;
        }

        using (response)
        {
            string jsonString = await response.Content.ReadAsStringAsync(token);

            if (jsonString.StartsWith(")]}'"))
            {
                jsonString = jsonString[4..];
            }
            
            JsonNode? json = JsonNode.Parse(jsonString);

            if (json is null)
            {
                return null;
            }
            
            // This is just an ordered (but unstructured) blob of data.
            string? value = json[0]?[2]?[0]?[0]?[13]?.ToString();

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    protected async Task<Result<JsonNode>> RetrieveJsonAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        using HttpResponseMessage response = await _youtubeClient.SendAsync(request, token);

        if (!response.IsSuccessStatusCode)
        {
            return Result<JsonNode>.Failure($"Unsuccessful HTTP response: {response.StatusCode}");
        }

        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>(token);

        return json is not null
            ? Result<JsonNode>.Success(json)
            : Result<JsonNode>.Failure("Failed to parse JSON response.");
    }

    protected static string ExtractVideoId(string videoUrl)
        => HttpUtility.ParseQueryString(new Uri(videoUrl).Query)["v"] ?? videoUrl;
}