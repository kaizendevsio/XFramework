namespace Fluid.Core.Models;

public class EntityColumn : BaseModel
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool? Unique { get; set; }
    public string? MaxLength { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    
}