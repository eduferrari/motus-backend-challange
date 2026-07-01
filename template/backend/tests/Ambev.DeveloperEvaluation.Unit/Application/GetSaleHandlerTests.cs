using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Domain.ReadModel;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class GetSaleHandlerTests
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public GetSaleHandlerTests()
    {
        _saleReadRepository = Substitute.For<ISaleReadRepository>();
        _mapper = Substitute.For<IMapper>();
    }

    [Fact(DisplayName = "Given existing sale id When getting Then returns SaleResult from MongoDB read model")]
    public async Task Handle_ExistingSale_ReturnsMappedResult()
    {
        var saleId = Guid.NewGuid();
        var document = new SaleDocument { Id = saleId, SaleNumber = "S-GET-001", TotalAmount = 400m };
        var expected = new SaleResult { Id = saleId, SaleNumber = "S-GET-001", TotalAmount = 400m };

        _saleReadRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(document);
        _mapper.Map<SaleResult>(document).Returns(expected);

        var handler = new GetSaleHandler(_saleReadRepository, _mapper);
        var response = await handler.Handle(new GetSaleCommand { Id = saleId }, CancellationToken.None);

        response.Should().Be(expected);
        await _saleReadRepository.Received(1).GetByIdAsync(saleId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent sale id When getting Then throws KeyNotFoundException")]
    public async Task Handle_SaleNotFound_ThrowsKeyNotFoundException()
    {
        var saleId = Guid.NewGuid();
        _saleReadRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns((SaleDocument?)null);

        var handler = new GetSaleHandler(_saleReadRepository, _mapper);
        var act = () => handler.Handle(new GetSaleCommand { Id = saleId }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{saleId}*");
    }
}
