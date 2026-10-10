using InventoryHold.Domain.Repositories;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.MongoDB;

public sealed class MongoInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly IMongoClient _client;
    private readonly MongoSessionContext _sessionContext;

    public MongoInventoryUnitOfWork(
        IMongoClient client,
        MongoSessionContext sessionContext)
    {
        _client = client;
        _sessionContext = sessionContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_sessionContext.CurrentSession is not null)
        {
            return await operation(cancellationToken);
        }

        using var session =
            await _client.StartSessionAsync(
                cancellationToken: cancellationToken);

        session.StartTransaction();
        _sessionContext.CurrentSession = session;

        try
        {
            var result = await operation(cancellationToken);

            await session.CommitTransactionAsync(cancellationToken);

            return result;
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(
                    cancellationToken);
            }

            throw;
        }
        finally
        {
            _sessionContext.CurrentSession = null;
        }
    }
}
