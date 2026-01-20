using System.Diagnostics.CodeAnalysis;
using NetCord;
using NetCord.Hosting.Gateway;

namespace Suruga.Primitives;

/// <summary>
/// Stores the bot's own user information once known.
/// Acts as a global, reference available to all types inheriting from <see cref="IGatewayHandler"/>.
/// </summary>
internal sealed class BotState
{
    public CurrentUser? User => _user;

    private CurrentUser? _user;

    [MemberNotNull(nameof(_user))]
    internal void Set(CurrentUser user)
        => Interlocked.Exchange(ref _user, user);
}
