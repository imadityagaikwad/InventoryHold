using MongoDB.Driver;

namespace InventoryHold.Infrastructure.MongoDB;

public sealed class MongoSessionContext
{
    public IClientSessionHandle? CurrentSession { get; set; }
}
