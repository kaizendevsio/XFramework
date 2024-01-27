namespace XFramework.Client.Shared.Core.Features;

public record QueryAction : StateAction
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string? OrderBy { get; set; }
    public OrderByDirection OrderByDirection { get; set; }
    public string? SearchText { get; set; }
    public List<string>? SearchFields { get; set; } = new();
    public bool Silent { get; set; }
}