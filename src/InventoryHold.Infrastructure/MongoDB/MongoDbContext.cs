using InventoryHold.Infrastructure.MongoDB.Documents;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.MongoDB;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<InventoryDocument> Inventory =>
        _database.GetCollection<InventoryDocument>("inventory");

    public IMongoCollection<HoldDocument> Holds =>
        _database.GetCollection<HoldDocument>("holds");
}
