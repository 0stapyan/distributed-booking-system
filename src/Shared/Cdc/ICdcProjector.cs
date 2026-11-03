using System.Text.Json;

namespace Shared.Cdc;

public interface ICdcProjector
{
    Task ProjectAsync(JsonElement row, long lsn, bool deleted, CancellationToken ct);
}