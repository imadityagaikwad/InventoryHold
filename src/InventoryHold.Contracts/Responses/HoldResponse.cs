namespace InventoryHold.Contracts.Responses;

public sealed record HoldResponse(
    string Id,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    IReadOnlyList<HoldItemResponse> Items);

public sealed record HoldItemResponse(
    string ProductId,
    string ProductName,
    int Quantity);
