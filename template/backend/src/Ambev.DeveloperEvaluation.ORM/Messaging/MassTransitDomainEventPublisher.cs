using Ambev.DeveloperEvaluation.Domain.Events;
using MassTransit;

namespace Ambev.DeveloperEvaluation.ORM.Messaging;

public class MassTransitDomainEventPublisher : IDomainEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitDomainEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : class
        => _publishEndpoint.Publish(domainEvent, cancellationToken);
}
