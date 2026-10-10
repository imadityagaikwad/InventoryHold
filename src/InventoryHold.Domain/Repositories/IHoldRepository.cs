using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Enums;

namespace InventoryHold.Domain.Repositories;

public interface IHoldRepository
{
    Task CreateAsync(
        InventoryHoldEntity hold,
        CancellationToken cancellationToken = default);

    Task<InventoryHoldEntity?> GetByIdAsync(
        string holdId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryHoldEntity>> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<bool> TryTransitionStatusAsync(
        string holdId,
        HoldStatus expectedStatus,
        HoldStatus newStatus,
        CancellationToken cancellationToken = default);
}
