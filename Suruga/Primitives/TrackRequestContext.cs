using System.Text.Json.Serialization;
using NetCord;

namespace Suruga.Primitives;

internal sealed record TrackRequestContext
{
	[JsonInclude]
	internal string Name { get; }
	
	[JsonInclude]
	internal string AvatarUrl { get; }
	
	[JsonConstructor]
	internal TrackRequestContext(string name, string avatarUrl)
	{
		Name = name;
		AvatarUrl = avatarUrl;
	}
	
    internal static TrackRequestContext FromUser(GuildUser user)
	{
		string name = user.Nickname ?? user.GlobalName ?? user.Username;
		string avatarUrl = (user.GetGuildAvatarUrl() ?? user.GetAvatarUrl() ?? user.DefaultAvatarUrl).ToString();
		
		return new TrackRequestContext(name, avatarUrl);
	}
}
