namespace UD_Autos.Models.Common;

public class PagerViewModel
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string Action { get; set; } = "Index";
    public object? RouteValues { get; set; }

    public static PagerViewModel From<T>(PagedResult<T> result, string action = "Index", object? routeValues = null)
        => new()
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            TotalCount = result.TotalCount,
            Action = action,
            RouteValues = routeValues
        };
}
