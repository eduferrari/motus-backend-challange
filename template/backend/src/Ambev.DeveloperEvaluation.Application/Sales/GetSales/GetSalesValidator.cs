using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSales;

public class GetSalesValidator : AbstractValidator<GetSalesCommand>
{
    public GetSalesValidator()
    {
        RuleFor(s => s.Page).GreaterThan(0);
        RuleFor(s => s.Size).InclusiveBetween(1, 100);
        RuleFor(s => s.MaxSaleDate)
            .GreaterThanOrEqualTo(s => s.MinSaleDate!.Value)
            .When(s => s.MinSaleDate.HasValue && s.MaxSaleDate.HasValue);
    }
}
