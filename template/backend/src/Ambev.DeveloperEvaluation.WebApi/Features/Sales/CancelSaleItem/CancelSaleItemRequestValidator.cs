using FluentValidation;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales.CancelSaleItem;

public class CancelSaleItemRequestValidator : AbstractValidator<CancelSaleItemRequest>
{
    public CancelSaleItemRequestValidator()
    {
        RuleFor(s => s.SaleId).NotEmpty();
        RuleFor(s => s.ItemId).NotEmpty();
    }
}
