using System.Text.Json;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Services;
using StackExchange.Redis;

namespace InventoryHold.Infrastructure.Redis;

public sealed class RedisInventoryCache : IInventoryCache
{
    private const string InventoryCacheKey = "inventory:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(20);

    private readonly IDatabase _database;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public RedisInventoryCache(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<IReadOnlyList<InventoryItem>?> GetInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cachedValue = await _database.StringGetAsync(InventoryCacheKey);

        if (cachedValue.IsNullOrEmpty)
            return null;

        var cachedItems =
            JsonSerializer.Deserialize<List<InventoryCacheItem>>(
                cachedValue.ToString(), JsonOptions);

        return cachedItems?
            .Select(item => new InventoryItem(
                item.ProductId, item.Name, item.AvailableQuantity))
            .ToList();
    }

    public async Task SetInventoryAsync(
        IReadOnlyList<InventoryItem> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheItems = items.Select(item => new InventoryCacheItem(
            item.ProductId,
            item.Name,
            item.AvailableQuantity)).ToList();

        var json = JsonSerializer.Serialize(cacheItems, JsonOptions);

        await _database.StringSetAsync(
            InventoryCacheKey, json, CacheDuration);
    }

    public async Task InvalidateInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.KeyDeleteAsync(InventoryCacheKey);
    }

    private sealed record InventoryCacheItem(
        string ProductId,
        string Name,
        int AvailableQuantity);
}
