using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Events;
using StackExchange.Redis;

namespace Ambev.DeveloperEvaluation.ORM.Messaging;

public class RedisDomainEventPublisher : IDomainEventPublisher
{
    private readonly IConnectionMultiplexer _redis;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisDomainEventPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var channel = GetChannelName<TEvent>();
        var payload = JsonSerializer.Serialize(domainEvent, SerializerOptions);
        await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), payload);
    }

    // "SaleCreatedEvent" → "sale.created"
    private static string GetChannelName<TEvent>()
    {
        var name = typeof(TEvent).Name;
        if (name.EndsWith("Event", StringComparison.Ordinal))
            name = name[..^"Event".Length];

        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "." + char.ToLower(c) : char.ToLower(c).ToString()));
    }
}
