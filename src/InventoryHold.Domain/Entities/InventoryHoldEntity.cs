using InventoryHold.Domain.Enums;

namespace InventoryHold.Domain.Entities;

public sealed class InventoryHoldEntity
{
    public string Id { get; private set; }
    public IReadOnlyList<HoldLineItem> Items { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public HoldStatus Status { get; private set; }

    public InventoryHoldEntity(
        string id,
        IEnumerable<HoldLineItem> items,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException(
                "Hold ID is required.", nameof(id));

        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();

        if (itemList.Count == 0)
            throw new ArgumentException(
                "A hold must contain at least one item.",
                nameof(items));

        if (itemList.Any(item =>
                item is null ||
                string.IsNullOrWhiteSpace(item.ProductId) ||
                item.Quantity <= 0))
        {
            throw new ArgumentException(
                "Each hold item must have a product ID and positive quantity.",
                nameof(items));
        }

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException(
                "Expiration must be later than creation time.",
                nameof(expiresAtUtc));

        Id = id;
        Items = itemList.AsReadOnly();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = HoldStatus.Active;
    }

    public bool IsExpired(DateTime utcNow) =>
        utcNow >= ExpiresAtUtc;

    public void Release()
    {
        if (Status != HoldStatus.Active)
            throw new InvalidOperationException(
                "Only an active hold can be released.");

        Status = HoldStatus.Released;
    }

    public void Expire(DateTime utcNow)
    {
        if (Status != HoldStatus.Active)
            throw new InvalidOperationException(
                "Only an active hold can expire.");

        if (!IsExpired(utcNow))
            throw new InvalidOperationException(
                "The hold has not expired.");

        Status = HoldStatus.Expired;
    }

    public InventoryHoldEntity WithStatus(HoldStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        Status = status;
        return this;
    }
}
