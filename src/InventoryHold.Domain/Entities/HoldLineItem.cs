namespace InventoryHold.Domain.Entities;

public sealed record HoldLineItem(
    string ProductId,
    string ProductName,
    int Quantity);
