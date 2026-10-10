using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Repositories;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByIdAsync(
        string productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> TryDeductAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken = default);
}
