using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Enums;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using Moq;

namespace InventoryHold.UnitTests.Services;

[TestFixture]
public sealed class HoldServiceTests
{
    private Mock<IInventoryRepository> _inventory = null!;
    private Mock<IHoldRepository> _holds = null!;
    private Mock<IInventoryCache> _cache = null!;
    private Mock<IHoldEventPublisher> _events = null!;
    private HoldService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new Mock<IInventoryRepository>();
        _holds = new Mock<IHoldRepository>();
        _cache = new Mock<IInventoryCache>();
        _events = new Mock<IHoldEventPublisher>();

        _cache
            .Setup(x => x.InvalidateInventoryAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _events
            .Setup(x => x.PublishCreatedAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new HoldService(
            _inventory.Object,
            _holds.Object,
            new InlineUnitOfWork(),
            _cache.Object,
            _events.Object,
            TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task CreateAsync_ValidRequest_CreatesHoldAndPublishesEvent()
    {
        var product = new InventoryItem("P100", "Keyboard", 10);
        InventoryHoldEntity? savedHold = null;

        _inventory
            .Setup(x => x.GetByIdAsync(
                "P100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _inventory
            .Setup(x => x.TryDeductAsync(
                "P100", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _holds
            .Setup(x => x.CreateAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryHoldEntity, CancellationToken>(
                (hold, _) => savedHold = hold)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(
            new[] { (ProductId: "P100", Quantity: 2) });

        Assert.That(result.Status.ToString(), Is.EqualTo("Active"));
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Quantity, Is.EqualTo(2));
        Assert.That(savedHold, Is.Not.Null);

        _inventory.Verify(
            x => x.TryDeductAsync(
                "P100", 2, It.IsAny<CancellationToken>()),
            Times.Once);

        _holds.Verify(
            x => x.CreateAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _events.Verify(
            x => x.PublishCreatedAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cache.Verify(
            x => x.InvalidateInventoryAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CreateAsync_InsufficientStock_ThrowsAndDoesNotCreateHold()
    {
        var product = new InventoryItem("P100", "Keyboard", 1);

        _inventory
            .Setup(x => x.GetByIdAsync(
                "P100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _inventory
            .Setup(x => x.TryDeductAsync(
                "P100", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateAsync(
                new[] { (ProductId: "P100", Quantity: 2) }));

        _holds.Verify(
            x => x.CreateAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _events.Verify(
            x => x.PublishCreatedAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase("", 1)]
    [TestCase("P100", 0)]
    [TestCase("P100", -1)]
    public void CreateAsync_InvalidItem_ThrowsArgumentException(
        string productId, int quantity)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateAsync(
                new[] { (ProductId: productId, Quantity: quantity) }));

        _inventory.Verify(
            x => x.GetByIdAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public void CreateAsync_EmptyRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateAsync(
                Array.Empty<(string ProductId, int Quantity)>()));

        _holds.Verify(
            x => x.CreateAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }


    [Test]
    public async Task ReleaseAsync_ActiveHold_RestoresInventoryAndPublishesEvent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var hold = new InventoryHoldEntity(
            "hold-123",
            new[]
            {
            new HoldLineItem("P100", "Keyboard", 2),
            new HoldLineItem("P200", "Mouse", 3)
            },
            now.AddMinutes(-1),
            now.AddMinutes(5));

        _holds
            .Setup(x => x.GetByIdAsync(
                "hold-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        _holds
            .Setup(x => x.TryTransitionStatusAsync(
                "hold-123",
                HoldStatus.Active,
                HoldStatus.Released,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _inventory
            .Setup(x => x.RestoreAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ReleaseAsync("hold-123");

        // Assert
        Assert.That(result, Is.True);

        _holds.Verify(
            x => x.TryTransitionStatusAsync(
                "hold-123",
                HoldStatus.Active,
                HoldStatus.Released,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _inventory.Verify(
            x => x.RestoreAsync(
                "P100", 2, It.IsAny<CancellationToken>()),
            Times.Once);

        _inventory.Verify(
            x => x.RestoreAsync(
                "P200", 3, It.IsAny<CancellationToken>()),
            Times.Once);

        _events.Verify(
            x => x.PublishReleasedAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cache.Verify(
            x => x.InvalidateInventoryAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ReleaseAsync_AlreadyReleasedHold_DoesNotRestoreInventoryOrRepublish()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var hold = new InventoryHoldEntity(
            "hold-123",
            new[]
            {
            new HoldLineItem("P100", "Keyboard", 2)
            },
            now.AddMinutes(-2),
            now.AddMinutes(5));

        hold.Release();

        _holds
            .Setup(x => x.GetByIdAsync(
                "hold-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        // Act
        var result = await _service.ReleaseAsync("hold-123");

        // Assert
        Assert.That(result, Is.True);

        _holds.Verify(
            x => x.TryTransitionStatusAsync(
                It.IsAny<string>(),
                It.IsAny<HoldStatus>(),
                It.IsAny<HoldStatus>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _inventory.Verify(
            x => x.RestoreAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _events.Verify(
            x => x.PublishReleasedAsync(
                It.IsAny<InventoryHoldEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _cache.Verify(
            x => x.InvalidateInventoryAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }


    private sealed class InlineUnitOfWork : IInventoryUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }
}
