using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Services;

public interface IHoldEventPublisher
{
    Task PublishCreatedAsync(
        InventoryHoldEntity hold,
        CancellationToken cancellationToken = default);

    Task PublishReleasedAsync(
        InventoryHoldEntity hold,
        CancellationToken cancellationToken = default);

    Task PublishExpiredAsync(
        InventoryHoldEntity hold,
        CancellationToken cancellationToken = default);
}
