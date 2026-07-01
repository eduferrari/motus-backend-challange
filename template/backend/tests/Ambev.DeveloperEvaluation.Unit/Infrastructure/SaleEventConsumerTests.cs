using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.ReadModel;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM.Consumers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public class SaleEventConsumerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISubscriber _subscriber;
    private readonly IConnectionMultiplexer _redis;
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IServiceScopeFactory _scopeFactory;

    public SaleEventConsumerTests()
    {
        _subscriber = Substitute.For<ISubscriber>();
        _redis = Substitute.For<IConnectionMultiplexer>();
        _redis.GetSubscriber().Returns(_subscriber);

        _saleRepository = Substitute.For<ISaleRepository>();
        _saleReadRepository = Substitute.For<ISaleReadRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISaleRepository)).Returns(_saleRepository);
        serviceProvider.GetService(typeof(ISaleReadRepository)).Returns(_saleReadRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    [Fact(DisplayName = "Given SaleCreated message When consumed Then upserts SaleDocument in MongoDB")]
    public async Task SaleCreatedEvent_Consumed_UpsertsDocumentInMongo()
    {
        var saleId = Guid.NewGuid();
        var sale = CreateSale(saleId, "S-001");

        _saleRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(sale);

        var callback = await StartAndCaptureCallback("sale.created");

        var evt = new SaleCreatedEvent { SaleId = saleId, SaleNumber = "S-001" };
        callback.Invoke(RedisChannel.Literal("sale.created"), Serialize(evt));

        await Task.Delay(50);

        await _saleReadRepository.Received(1).UpsertAsync(
            Arg.Is<SaleDocument>(d => d.Id == saleId && d.SaleNumber == "S-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given SaleModified message When consumed Then upserts updated SaleDocument")]
    public async Task SaleModifiedEvent_Consumed_UpsertsDocumentInMongo()
    {
        var saleId = Guid.NewGuid();
        var sale = CreateSale(saleId, "S-002");

        _saleRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(sale);

        var callback = await StartAndCaptureCallback("sale.modified");

        var evt = new SaleModifiedEvent { SaleId = saleId, SaleNumber = "S-002" };
        callback.Invoke(RedisChannel.Literal("sale.modified"), Serialize(evt));

        await Task.Delay(50);

        await _saleReadRepository.Received(1).UpsertAsync(
            Arg.Is<SaleDocument>(d => d.Id == saleId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given SaleCancelled message When consumed Then marks document as cancelled")]
    public async Task SaleCancelledEvent_Consumed_MarksDocumentCancelled()
    {
        var saleId = Guid.NewGuid();
        var document = new SaleDocument { Id = saleId, IsCancelled = false };

        _saleReadRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(document);

        var callback = await StartAndCaptureCallback("sale.cancelled");

        var evt = new SaleCancelledEvent { SaleId = saleId, OccurredAt = DateTime.UtcNow };
        callback.Invoke(RedisChannel.Literal("sale.cancelled"), Serialize(evt));

        await Task.Delay(50);

        await _saleReadRepository.Received(1).UpsertAsync(
            Arg.Is<SaleDocument>(d => d.Id == saleId && d.IsCancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given ItemCancelled message When consumed Then marks item as cancelled in document")]
    public async Task ItemCancelledEvent_Consumed_MarksItemCancelled()
    {
        var saleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var document = new SaleDocument
        {
            Id = saleId,
            Items = [new SaleItemDocument { Id = itemId, IsCancelled = false }]
        };

        _saleReadRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(document);

        var callback = await StartAndCaptureCallback("item.cancelled");

        var evt = new ItemCancelledEvent { SaleId = saleId, ItemId = itemId, OccurredAt = DateTime.UtcNow };
        callback.Invoke(RedisChannel.Literal("item.cancelled"), Serialize(evt));

        await Task.Delay(50);

        await _saleReadRepository.Received(1).UpsertAsync(
            Arg.Is<SaleDocument>(d => d.Items.Single(i => i.Id == itemId).IsCancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given invalid JSON message When consumed Then logs error and does not throw")]
    public async Task InvalidMessage_Consumed_LogsErrorAndDoesNotThrow()
    {
        var callback = await StartAndCaptureCallback("sale.created");

        var act = () =>
        {
            callback.Invoke(RedisChannel.Literal("sale.created"), "not-valid-json");
            return Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
        await _saleReadRepository.DidNotReceive().UpsertAsync(Arg.Any<SaleDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given SaleCreated for unknown sale When consumed Then skips upsert gracefully")]
    public async Task SaleCreatedEvent_SaleNotInPostgres_SkipsUpsert()
    {
        _saleRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var callback = await StartAndCaptureCallback("sale.created");

        var evt = new SaleCreatedEvent { SaleId = Guid.NewGuid() };
        callback.Invoke(RedisChannel.Literal("sale.created"), Serialize(evt));

        await Task.Delay(50);

        await _saleReadRepository.DidNotReceive().UpsertAsync(Arg.Any<SaleDocument>(), Arg.Any<CancellationToken>());
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<Action<RedisChannel, RedisValue>> StartAndCaptureCallback(string channel)
    {
        Action<RedisChannel, RedisValue>? captured = null;

        await _subscriber.SubscribeAsync(
            Arg.Is<RedisChannel>(c => c == RedisChannel.Literal(channel)),
            Arg.Do<Action<RedisChannel, RedisValue>>(cb => captured = cb));

        var consumer = new SaleEventConsumer(_redis, _scopeFactory, NullLogger<SaleEventConsumer>.Instance);
        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);

        captured.Should().NotBeNull("subscription should have been registered during StartAsync");
        return captured!;
    }

    private static string Serialize<T>(T obj)
        => JsonSerializer.Serialize(obj, JsonOptions);

    private static Sale CreateSale(Guid id, string saleNumber)
    {
        var sale = new Sale(
            saleNumber,
            DateTime.UtcNow,
            Guid.NewGuid(), "Customer",
            Guid.NewGuid(), "Branch",
            [new SaleItem(Guid.NewGuid(), "Product", 4, 100)]);

        typeof(Sale).GetProperty(nameof(Sale.Id))!.SetValue(sale, id);
        return sale;
    }
}
