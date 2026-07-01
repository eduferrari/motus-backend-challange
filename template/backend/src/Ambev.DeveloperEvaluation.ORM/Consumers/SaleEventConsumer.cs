using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.ReadModel;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Ambev.DeveloperEvaluation.ORM.Consumers;

public class SaleEventConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SaleEventConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SaleEventConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<SaleEventConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(RedisChannel.Literal("sale.created"), async (_, message) =>
            await HandleAsync<SaleCreatedEvent>(message, ProjectSaleAsync, stoppingToken));

        await subscriber.SubscribeAsync(RedisChannel.Literal("sale.modified"), async (_, message) =>
            await HandleAsync<SaleModifiedEvent>(message, ProjectSaleAsync, stoppingToken));

        await subscriber.SubscribeAsync(RedisChannel.Literal("sale.cancelled"), async (_, message) =>
            await HandleAsync<SaleCancelledEvent>(message, ProjectCancelledAsync, stoppingToken));

        await subscriber.SubscribeAsync(RedisChannel.Literal("item.cancelled"), async (_, message) =>
            await HandleAsync<ItemCancelledEvent>(message, ProjectItemCancelledAsync, stoppingToken));

        // Keep alive until the host shuts down
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task HandleAsync<TEvent>(
        RedisValue message,
        Func<TEvent, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<TEvent>(message.ToString(), JsonOptions);
            if (evt is null) return;
            await handler(evt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {EventType} message", typeof(TEvent).Name);
        }
    }

    private async Task ProjectSaleAsync(SaleCreatedEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var saleRepo = scope.ServiceProvider.GetRequiredService<ISaleRepository>();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISaleReadRepository>();

        var sale = await saleRepo.GetByIdAsync(evt.SaleId, ct);
        if (sale is null) return;

        var document = new SaleDocument
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            SaleDate = sale.SaleDate,
            CustomerId = sale.CustomerId,
            CustomerName = sale.CustomerName,
            BranchId = sale.BranchId,
            BranchName = sale.BranchName,
            TotalAmount = sale.TotalAmount,
            IsCancelled = sale.IsCancelled,
            CreatedAt = sale.CreatedAt,
            UpdatedAt = sale.UpdatedAt,
            Items = sale.Items.Select(i => new SaleItemDocument
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Discount = i.Discount,
                TotalAmount = i.TotalAmount,
                IsCancelled = i.IsCancelled
            }).ToList()
        };

        await readRepo.UpsertAsync(document, ct);
        _logger.LogInformation("Projected SaleCreated into MongoDB: {SaleId}", evt.SaleId);
    }

    private async Task ProjectSaleAsync(SaleModifiedEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var saleRepo = scope.ServiceProvider.GetRequiredService<ISaleRepository>();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISaleReadRepository>();

        var sale = await saleRepo.GetByIdAsync(evt.SaleId, ct);
        if (sale is null) return;

        var document = new SaleDocument
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            SaleDate = sale.SaleDate,
            CustomerId = sale.CustomerId,
            CustomerName = sale.CustomerName,
            BranchId = sale.BranchId,
            BranchName = sale.BranchName,
            TotalAmount = sale.TotalAmount,
            IsCancelled = sale.IsCancelled,
            CreatedAt = sale.CreatedAt,
            UpdatedAt = sale.UpdatedAt,
            Items = sale.Items.Select(i => new SaleItemDocument
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Discount = i.Discount,
                TotalAmount = i.TotalAmount,
                IsCancelled = i.IsCancelled
            }).ToList()
        };

        await readRepo.UpsertAsync(document, ct);
        _logger.LogInformation("Projected SaleModified into MongoDB: {SaleId}", evt.SaleId);
    }

    private async Task ProjectCancelledAsync(SaleCancelledEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISaleReadRepository>();

        var document = await readRepo.GetByIdAsync(evt.SaleId, ct);
        if (document is null) return;

        document.IsCancelled = true;
        document.UpdatedAt = evt.OccurredAt;

        await readRepo.UpsertAsync(document, ct);
        _logger.LogInformation("Projected SaleCancelled into MongoDB: {SaleId}", evt.SaleId);
    }

    private async Task ProjectItemCancelledAsync(ItemCancelledEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var readRepo = scope.ServiceProvider.GetRequiredService<ISaleReadRepository>();

        var document = await readRepo.GetByIdAsync(evt.SaleId, ct);
        if (document is null) return;

        var item = document.Items.FirstOrDefault(i => i.Id == evt.ItemId);
        if (item is not null)
            item.IsCancelled = true;

        document.UpdatedAt = evt.OccurredAt;

        await readRepo.UpsertAsync(document, ct);
        _logger.LogInformation("Projected ItemCancelled into MongoDB: {ItemId}", evt.ItemId);
    }
}
