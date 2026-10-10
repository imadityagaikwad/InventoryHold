
using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Services;

public interface IInventoryCache
{
    Task<IReadOnlyList<InventoryItem>?> GetInventoryAsync(
        CancellationToken cancellationToken = default);

    Task SetInventoryAsync(
        IReadOnlyList<InventoryItem> items,
        CancellationToken cancellationToken = default);

    Task InvalidateInventoryAsync(
        CancellationToken cancellationToken = default);
}
