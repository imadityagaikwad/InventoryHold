using System.Text;
using System.Text.Json;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Services;
using RabbitMQ.Client;

namespace InventoryHold.Infrastructure.Messaging;

public sealed class RabbitMqHoldEventPublisher : IHoldEventPublisher, IAsyncDisposable
{
    private const string ExchangeName = "inventory.holds";
    private const string QueueName = "inventory.hold.events";

    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqHoldEventPublisher(string connectionString)
    {
        _factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString)
        };
    }

    public Task PublishCreatedAsync(
        InventoryHoldEntity hold, CancellationToken cancellationToken) =>
        PublishAsync("hold.created", hold, cancellationToken);

    public Task PublishReleasedAsync(
        InventoryHoldEntity hold, CancellationToken cancellationToken) =>
        PublishAsync("hold.released", hold, cancellationToken);

    public Task PublishExpiredAsync(
        InventoryHoldEntity hold, CancellationToken cancellationToken) =>
        PublishAsync("hold.expired", hold, cancellationToken);

    private async Task PublishAsync(
        string routingKey,
        InventoryHoldEntity hold,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null || !_connection.IsOpen ||
                _channel is null || !_channel.IsOpen)
            {
                if (_channel is not null)
                    await _channel.DisposeAsync();

                if (_connection is not null)
                    await _connection.DisposeAsync();

                _connection = await _factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(
                    cancellationToken: cancellationToken);

                await _channel.ExchangeDeclareAsync(
                    exchange: ExchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken);

                await _channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: cancellationToken);

                foreach (var key in new[]
                {
                    "hold.created",
                    "hold.released",
                    "hold.expired"
                })
                {
                    await _channel.QueueBindAsync(
                        queue: QueueName,
                        exchange: ExchangeName,
                        routingKey: key,
                        cancellationToken: cancellationToken);
                }
            }

            var payload = JsonSerializer.Serialize(new
            {
                EventType = routingKey,
                hold.Id,
                hold.Status,
                hold.CreatedAtUtc,
                hold.ExpiresAtUtc,
                Items = hold.Items.Select(item => new
                {
                    item.ProductId,
                    item.ProductName,
                    item.Quantity
                })
            });

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString("N"),
                Type = routingKey,
                Timestamp = new AmqpTimestamp(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payload),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_channel is not null)
                await _channel.DisposeAsync();

            if (_connection is not null)
                await _connection.DisposeAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
