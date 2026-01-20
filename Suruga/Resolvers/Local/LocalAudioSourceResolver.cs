using System.Text.Json.Nodes;
using Suruga.IPC;
using Suruga.Primitives;
using Suruga.Resolvers.Abstractions;
using Suruga.Resolvers.Inputs;
using Suruga.Transport.Primitives;

namespace Suruga.Resolvers.Local;

internal sealed class LocalAudioSourceResolver : IAudioSourceResolver
{
    public async Task<Result<AudioSource>> ResolveAsync(string input, CancellationToken token = default)
    {
        if (!InputValidator.TryValidate(input, out _))
        {
            return Result<AudioSource>.Failure($"File '{input}' is not an audio file.");
        }

        string fileName = Path.GetFileNameWithoutExtension(input);
        Result<AsyncProcess> ffprobeResult = await FFprobeAsyncProcess.StartAsync(fileName, token);

        if (!ffprobeResult.TryGetValue(out AsyncProcess? ffprobe))
        {
            return Result<AudioSource>.Failure(ffprobeResult.Error);
        }

        using (ffprobe)
        {
            await using Stream jsonStream = ffprobe.StandardOutput;
            JsonNode? root = await JsonNode.ParseAsync(jsonStream, cancellationToken: token);

            if (root is null)
            {
                return Result<AudioSource>.Failure("FFprobe output is null.");
            }

            JsonArray? tags = root["format"]?["tags"]?.AsArray();

            string title = tags?["title"]?.ToString() ?? fileName;

            string author = tags?["artist"]?.ToString()
                ?? tags?["album_artist"]?.ToString()
                ?? "Unknown";

            string? duration = root["format"]?["duration"]?.ToString();

            AudioSource source = AudioSource.FromSingle(AudioPlatform.Local, new AudioTrack
            {
                Stream = new LocalAudioStreamDescriptor(input),
                Platform = AudioPlatform.Local,
                Title = title,
                Author = author,
                Duration = TimeSpan.TryParse(duration, out TimeSpan trackDuration) ? trackDuration : null
            });
            
            return Result<AudioSource>.Success(source);
        }
    }
}
