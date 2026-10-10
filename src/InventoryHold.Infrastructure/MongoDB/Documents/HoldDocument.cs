using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using InventoryHold.Domain.Enums;

namespace InventoryHold.Infrastructure.MongoDB.Documents;

public sealed class HoldDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    public List<HoldDocumentItem> Items { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    [BsonRepresentation(BsonType.String)]
    public HoldStatus Status { get; set; }
}

public sealed class HoldDocumentItem
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
