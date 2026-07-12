using NetCord;

namespace Suruga.Primitives;

/// <summary>
/// Represents the context of the user who requested a track, including their display name and avatar.
/// </summary>
/// <param name="Username">The display name of the user who requested the track.</param>
/// <param name="AvatarUrl">The URL of the user's avatar.</param>
public readonly record struct TrackRequestContext(string Username, string AvatarUrl)
{
    /// <summary>
    /// Creates a new <see cref="TrackRequestContext"/> from a guild user, 
    /// resolving the most appropriate display name and avatar URL.
    /// </summary>
    /// <param name="user">The guild user to extract the context from.</param>
    /// <returns>
    /// A new <see cref="TrackRequestContext"/> populated with the user's resolved name and avatar URL.
    /// </returns>
    internal static TrackRequestContext FromUser(GuildUser user)
	{
		string name = user.Nickname ?? user.GlobalName ?? user.Username;
		string avatarUrl = (user.GetGuildAvatarUrl() ?? user.GetAvatarUrl() ?? user.DefaultAvatarUrl).ToString();
		
		return new TrackRequestContext(name, avatarUrl);
	}
}
