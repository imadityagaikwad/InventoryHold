using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryHold.Infrastructure.MongoDB.Documents;

public sealed class InventoryDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }
}
