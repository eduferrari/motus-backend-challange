using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class CancelSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly IDomainEventPublisher _eventPublisher;

    public CancelSaleHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _eventPublisher = Substitute.For<IDomainEventPublisher>();
    }

    [Fact(DisplayName = "Given active sale When cancelling Then persists cancellation and publishes event")]
    public async Task Handle_ActiveSale_CancelsSaleAndPublishesEvent()
    {
        var sale = CreateActiveSale();
        var result = new SaleResult { Id = sale.Id, IsCancelled = true };

        _saleRepository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _saleRepository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<SaleResult>(Arg.Any<Sale>()).Returns(result);

        var handler = CreateHandler();
        var command = new CancelSaleCommand { Id = sale.Id };

        var response = await handler.Handle(command, CancellationToken.None);

        sale.IsCancelled.Should().BeTrue();
        response.IsCancelled.Should().BeTrue();
        await _saleRepository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<SaleCancelledEvent>(e => e.SaleId == sale.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given already cancelled sale When cancelling Then returns early without DB write or event")]
    public async Task Handle_AlreadyCancelledSale_ReturnsEarlyIdempotently()
    {
        var sale = CreateActiveSale();
        sale.Cancel();
        var result = new SaleResult { Id = sale.Id, IsCancelled = true };

        _saleRepository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _mapper.Map<SaleResult>(Arg.Any<Sale>()).Returns(result);

        var handler = CreateHandler();
        var command = new CancelSaleCommand { Id = sale.Id };

        var response = await handler.Handle(command, CancellationToken.None);

        response.IsCancelled.Should().BeTrue();
        await _saleRepository.DidNotReceive().UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<SaleCancelledEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given unknown sale id When cancelling Then throws KeyNotFoundException")]
    public async Task Handle_SaleNotFound_ThrowsKeyNotFoundException()
    {
        _saleRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var handler = CreateHandler();
        var act = () => handler.Handle(new CancelSaleCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*not found*");
    }

    private CancelSaleHandler CreateHandler() =>
        new(_saleRepository, _mapper, NullLogger<CancelSaleHandler>.Instance, _eventPublisher);

    private static Sale CreateActiveSale() =>
        new("S-CANCEL-001", DateTime.UtcNow,
            Guid.NewGuid(), "Customer",
            Guid.NewGuid(), "Branch",
            [new SaleItem(Guid.NewGuid(), "Product", 4, 100)]);
}
