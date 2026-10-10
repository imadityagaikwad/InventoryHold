using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Services;

public interface IHoldService
{
    Task<InventoryHoldEntity> CreateAsync(
        IEnumerable<(string ProductId, int Quantity)> requestedItems,
        CancellationToken cancellationToken = default);

    Task<InventoryHoldEntity?> GetByIdAsync(
        string holdId,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(
        string holdId,
        CancellationToken cancellationToken = default);

    Task<int> ProcessExpiredHoldsAsync(
        CancellationToken cancellationToken = default);
}
