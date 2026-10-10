namespace InventoryHold.Domain.Entities;

public sealed class InventoryItem
{
    public string ProductId { get; private set; }
    public string Name { get; private set; }
    public int AvailableQuantity { get; private set; }

    public InventoryItem(
        string productId,
        string name,
        int availableQuantity)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException(
                "Product ID is required.", nameof(productId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Product name is required.", nameof(name));

        if (availableQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(availableQuantity),
                "Available quantity cannot be negative.");

        ProductId = productId;
        Name = name;
        AvailableQuantity = availableQuantity;
    }

    public bool TryDeduct(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "Quantity must be positive.");

        if (AvailableQuantity < quantity)
            return false;

        AvailableQuantity -= quantity;
        return true;
    }

    public void Restore(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "Quantity must be positive.");

        AvailableQuantity = checked(AvailableQuantity + quantity);
    }
}
