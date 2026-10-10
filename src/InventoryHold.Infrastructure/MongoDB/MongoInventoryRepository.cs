using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using InventoryHold.Infrastructure.MongoDB.Documents;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.MongoDB;

public sealed class MongoInventoryRepository : IInventoryRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSessionContext _sessionContext;

    public MongoInventoryRepository(
        MongoDbContext context,
        MongoSessionContext sessionContext)
    {
        _context = context;
        _sessionContext = sessionContext;
    }

    public async Task<InventoryItem?> GetByIdAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<InventoryDocument>.Filter.Eq(
            x => x.Id, productId);

        var document = _sessionContext.CurrentSession is { } session
            ? await _context.Inventory
                .Find(session, filter)
                .FirstOrDefaultAsync(cancellationToken)
            : await _context.Inventory
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

        return document is null
            ? null
            : new InventoryItem(
                document.Id,
                document.Name,
                document.AvailableQuantity);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = _sessionContext.CurrentSession is { } session
            ? await _context.Inventory
                .Find(session, Builders<InventoryDocument>.Filter.Empty)
                .ToListAsync(cancellationToken)
            : await _context.Inventory
                .Find(Builders<InventoryDocument>.Filter.Empty)
                .ToListAsync(cancellationToken);

        return documents
            .Select(x => new InventoryItem(
                x.Id, x.Name, x.AvailableQuantity))
            .ToList();
    }

    public async Task<bool> TryDeductAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var filter = Builders<InventoryDocument>.Filter.And(
            Builders<InventoryDocument>.Filter.Eq(
                x => x.Id, productId),
            Builders<InventoryDocument>.Filter.Gte(
                x => x.AvailableQuantity, quantity));

        var update = Builders<InventoryDocument>.Update.Inc(
            x => x.AvailableQuantity, -quantity);

        var result = _sessionContext.CurrentSession is { } session
            ? await _context.Inventory.UpdateOneAsync(
                session, filter, update,
                cancellationToken: cancellationToken)
            : await _context.Inventory.UpdateOneAsync(
                filter, update,
                cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    public async Task RestoreAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var filter = Builders<InventoryDocument>.Filter.Eq(
            x => x.Id, productId);

        var update = Builders<InventoryDocument>.Update.Inc(
            x => x.AvailableQuantity, quantity);

        var result = _sessionContext.CurrentSession is { } session
            ? await _context.Inventory.UpdateOneAsync(
                session, filter, update,
                cancellationToken: cancellationToken)
            : await _context.Inventory.UpdateOneAsync(
                filter, update,
                cancellationToken: cancellationToken);

        if (result.MatchedCount != 1)
        {
            throw new InvalidOperationException(
                $"Inventory product '{productId}' was not found.");
        }
    }
}
