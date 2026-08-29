using NetCord;
using NetCord.Rest;

namespace Suruga.Commands.Extensions;

/// <summary>
/// Provides extension methods for <see cref="InteractionMessageProperties"/> to simplify common message responses.
/// </summary>
internal static class InteractionMessagePropertiesExtensions
{
	/// <summary>
	/// Configures the message properties to indicate that the application is not
	/// connected to a voice channel.
	/// </summary>
	/// <remarks>
	/// The response is ephemeral, visible only to the user who triggered the interaction.
	/// </remarks>
	/// <param name="properties">The message properties instance to modify.</param>
	/// <returns>The modified <see cref="InteractionMessageProperties"/> instance.</returns>
	internal static InteractionMessageProperties NotInVoiceChannelMessage(this InteractionMessageProperties properties)
		=> properties.WithContent("I must be connected to a voice channel.").WithFlags(MessageFlags.Ephemeral);
}
