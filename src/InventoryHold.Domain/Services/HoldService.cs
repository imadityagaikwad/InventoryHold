using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Enums;
using InventoryHold.Domain.Repositories;

namespace InventoryHold.Domain.Services;

public sealed class HoldService : IHoldService
{
    private readonly IInventoryRepository _inventory;
    private readonly IHoldRepository _holds;
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IInventoryCache _cache;
    private readonly IHoldEventPublisher _events;
    private readonly TimeSpan _holdDuration;
    private readonly TimeProvider _timeProvider;

    public HoldService(
        IInventoryRepository inventory,
        IHoldRepository holds,
        IInventoryUnitOfWork unitOfWork,
        IInventoryCache cache,
        IHoldEventPublisher events,
        TimeSpan holdDuration,
        TimeProvider? timeProvider = null)
    {
        if (holdDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(holdDuration));

        _inventory = inventory;
        _holds = holds;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _events = events;
        _holdDuration = holdDuration;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InventoryHoldEntity> CreateAsync(
        IEnumerable<(string ProductId, int Quantity)> requestedItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedItems);

        var items = requestedItems.ToList();

        if (items.Count == 0)
            throw new ArgumentException(
                "At least one item is required.", nameof(requestedItems));

        if (items.Any(x =>
                string.IsNullOrWhiteSpace(x.ProductId) || x.Quantity <= 0))
        {
            throw new ArgumentException(
                "Every item must have a product ID and positive quantity.",
                nameof(requestedItems));
        }

        // Combine duplicate product IDs before reserving stock.
        var aggregated = items
            .GroupBy(x => x.ProductId, StringComparer.Ordinal)
            .Select(g => (
                ProductId: g.Key,
                Quantity: g.Aggregate(
                    0,
                    (total, item) => checked(total + item.Quantity))))
            .ToList();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var hold = await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var holdItems = new List<HoldLineItem>();

                foreach (var requested in aggregated)
                {
                    var product = await _inventory.GetByIdAsync(
                        requested.ProductId, ct);

                    if (product is null)
                        throw new KeyNotFoundException(
                            $"Product '{requested.ProductId}' was not found.");

                    if (!await _inventory.TryDeductAsync(
                            requested.ProductId, requested.Quantity, ct))
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{product.Name}'.");
                    }

                    holdItems.Add(new HoldLineItem(
                        product.ProductId,
                        product.Name,
                        requested.Quantity));
                }

                var created = new InventoryHoldEntity(
                    Guid.NewGuid().ToString("N"),
                    holdItems,
                    now,
                    now.Add(_holdDuration));

                await _holds.CreateAsync(created, ct);

                return created;
            },
            cancellationToken);

        await _cache.InvalidateInventoryAsync(cancellationToken);
        await _events.PublishCreatedAsync(hold, cancellationToken);

        return hold;
    }

    public async Task<InventoryHoldEntity?> GetByIdAsync(
        string holdId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _holds.GetByIdAsync(holdId, cancellationToken);

        if (hold is null || hold.Status != HoldStatus.Active)
            return hold;

        if (!hold.IsExpired(_timeProvider.GetUtcNow().UtcDateTime))
            return hold;

        await TransitionActiveHoldAsync(
            holdId, expired: true, cancellationToken);

        return await _holds.GetByIdAsync(holdId, cancellationToken);
    }

    public async Task<bool> ReleaseAsync(
        string holdId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _holds.GetByIdAsync(holdId, cancellationToken);

        if (hold is null)
            return false;

        if (hold.Status != HoldStatus.Active)
            return true;

        var expired = hold.IsExpired(
            _timeProvider.GetUtcNow().UtcDateTime);

        await TransitionActiveHoldAsync(
            holdId, expired, cancellationToken);

        return true;
    }

    public async Task<int> ProcessExpiredHoldsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activeHolds = await _holds.GetActiveAsync(cancellationToken);
        var processed = 0;

        foreach (var hold in activeHolds.Where(x => x.IsExpired(now)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var transitioned = await TransitionActiveHoldAsync(
                hold.Id, expired: true, cancellationToken);

            if (transitioned)
                processed++;
        }

        return processed;
    }

    private async Task<bool> TransitionActiveHoldAsync(
        string holdId,
        bool expired,
        CancellationToken cancellationToken)
    {
        var hold = await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var current = await _holds.GetByIdAsync(holdId, ct);

                if (current is null || current.Status != HoldStatus.Active)
                    return null;

                // Recheck expiration inside the transaction.
                var shouldExpire = current.IsExpired(
                    _timeProvider.GetUtcNow().UtcDateTime);

                if (expired && !shouldExpire)
                    return null;

                var targetStatus = shouldExpire
                    ? HoldStatus.Expired
                    : HoldStatus.Released;

                if (!await _holds.TryTransitionStatusAsync(
                        holdId, HoldStatus.Active, targetStatus, ct))
                {
                    return null;
                }

                foreach (var item in current.Items)
                {
                    await _inventory.RestoreAsync(
                        item.ProductId, item.Quantity, ct);
                }

                return current.WithStatus(targetStatus);
            },
            cancellationToken);

        if (hold is null)
            return false;

        await _cache.InvalidateInventoryAsync(cancellationToken);

        if (hold.Status == HoldStatus.Expired)
            await _events.PublishExpiredAsync(hold, cancellationToken);
        else
            await _events.PublishReleasedAsync(hold, cancellationToken);

        return true;
    }
}
