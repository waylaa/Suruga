using Microsoft.Extensions.Options;
using Suruga.Options;

namespace Suruga.Persistence;

internal sealed class PersistenceAvailability(IOptions<DatabaseOptions> databaseOptions, IOptions<InvidiousCompanionOptions> invidiousCompanionOptions)
{
    internal bool IsEnabled => databaseOptions.Value.Enable && invidiousCompanionOptions.Value.Enable;
}
