using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.ORM.Messaging;
using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public class RedisDomainEventPublisherTests
{
    private readonly ISubscriber _subscriber;
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisDomainEventPublisher _publisher;

    public RedisDomainEventPublisherTests()
    {
        _subscriber = Substitute.For<ISubscriber>();
        _redis = Substitute.For<IConnectionMultiplexer>();
        _redis.GetSubscriber().Returns(_subscriber);
        _publisher = new RedisDomainEventPublisher(_redis);
    }

    [Theory(DisplayName = "Given event type When publishing Then uses correct Redis channel")]
    [InlineData(typeof(SaleCreatedEvent), "sale.created")]
    [InlineData(typeof(SaleModifiedEvent), "sale.modified")]
    [InlineData(typeof(SaleCancelledEvent), "sale.cancelled")]
    [InlineData(typeof(ItemCancelledEvent), "item.cancelled")]
    public async Task PublishAsync_AnyDomainEvent_PublishesToCorrectChannel(Type eventType, string expectedChannel)
    {
        var evt = Activator.CreateInstance(eventType)!;

        await (Task)typeof(RedisDomainEventPublisher)
            .GetMethod(nameof(RedisDomainEventPublisher.PublishAsync))!
            .MakeGenericMethod(eventType)
            .Invoke(_publisher, [evt, CancellationToken.None])!;

        await _subscriber.Received(1).PublishAsync(
            Arg.Is<RedisChannel>(c => c == RedisChannel.Literal(expectedChannel)),
            Arg.Any<RedisValue>());
    }

    [Fact(DisplayName = "Given SaleCreatedEvent When publishing Then payload is camelCase JSON")]
    public async Task PublishAsync_SaleCreatedEvent_SerializesPayloadAsCamelCaseJson()
    {
        var saleId = Guid.NewGuid();
        var evt = new SaleCreatedEvent
        {
            SaleId = saleId,
            SaleNumber = "S-001",
            CustomerName = "Acme Corp",
            TotalAmount = 1500m
        };

        RedisValue capturedPayload = default;
        await _subscriber.PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Do<RedisValue>(v => capturedPayload = v));

        await _publisher.PublishAsync(evt);

        var json = capturedPayload.ToString();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("saleId").GetGuid().Should().Be(saleId);
        doc.RootElement.GetProperty("saleNumber").GetString().Should().Be("S-001");
        doc.RootElement.GetProperty("totalAmount").GetDecimal().Should().Be(1500m);
    }

    [Fact(DisplayName = "Given Redis failure When publishing Then exception propagates")]
    public async Task PublishAsync_RedisThrows_ExceptionPropagates()
    {
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>())
            .Returns<long>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var act = () => _publisher.PublishAsync(new SaleCreatedEvent());

        await act.Should().ThrowAsync<RedisConnectionException>();
    }
}
