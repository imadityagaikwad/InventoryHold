using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Enums;
using InventoryHold.Domain.Repositories;
using InventoryHold.Infrastructure.MongoDB.Documents;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.MongoDB;

public sealed class MongoHoldRepository : IHoldRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSessionContext _sessionContext;

    public MongoHoldRepository(
        MongoDbContext context,
        MongoSessionContext sessionContext)
    {
        _context = context;
        _sessionContext = sessionContext;
    }

    public async Task CreateAsync(
        InventoryHoldEntity hold,
        CancellationToken cancellationToken = default)
    {
        var document = new HoldDocument
        {
            Id = hold.Id,
            Items = hold.Items.Select(item => new HoldDocumentItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity
            }).ToList(),
            CreatedAtUtc = hold.CreatedAtUtc,
            ExpiresAtUtc = hold.ExpiresAtUtc,
            Status = hold.Status
        };

        if (_sessionContext.CurrentSession is { } session)
        {
            await _context.Holds.InsertOneAsync(
                session,
                document,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _context.Holds.InsertOneAsync(
                document,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<InventoryHoldEntity?> GetByIdAsync(
        string holdId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<HoldDocument>.Filter.Eq(
            x => x.Id, holdId);

        var document = _sessionContext.CurrentSession is { } session
            ? await _context.Holds
                .Find(session, filter)
                .FirstOrDefaultAsync(cancellationToken)
            : await _context.Holds
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ToEntity(document);
    }

    public async Task<IReadOnlyList<InventoryHoldEntity>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<HoldDocument>.Filter.Eq(
            x => x.Status, HoldStatus.Active);

        var documents = _sessionContext.CurrentSession is { } session
            ? await _context.Holds
                .Find(session, filter)
                .ToListAsync(cancellationToken)
            : await _context.Holds
                .Find(filter)
                .ToListAsync(cancellationToken);

        return documents.Select(ToEntity).ToList();
    }

    public async Task<bool> TryTransitionStatusAsync(
        string holdId,
        HoldStatus expectedStatus,
        HoldStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        if (expectedStatus == newStatus)
        {
            throw new ArgumentException(
                "The new status must differ from the expected status.",
                nameof(newStatus));
        }

        var filter = Builders<HoldDocument>.Filter.And(
            Builders<HoldDocument>.Filter.Eq(x => x.Id, holdId),
            Builders<HoldDocument>.Filter.Eq(x => x.Status, expectedStatus));

        var update = Builders<HoldDocument>.Update.Set(
            x => x.Status, newStatus);

        var result = _sessionContext.CurrentSession is { } session
            ? await _context.Holds.UpdateOneAsync(
                session, filter, update,
                cancellationToken: cancellationToken)
            : await _context.Holds.UpdateOneAsync(
                filter, update,
                cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    private static InventoryHoldEntity ToEntity(HoldDocument document)
    {
        return new InventoryHoldEntity(
            document.Id,
            document.Items.Select(item => new HoldLineItem(
                item.ProductId,
                item.ProductName,
                item.Quantity)),
            DateTime.SpecifyKind(document.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(document.ExpiresAtUtc, DateTimeKind.Utc))
            .WithStatus(document.Status);
    }
}
