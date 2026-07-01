namespace Ambev.DeveloperEvaluation.Application.Common;

public class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = [];
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}
