namespace Fluid.Core.Models;

public class Entity : BaseModel
{
    public string? Name { get; set; }
    public ICollection<EntityColumn> Columns { get; set; } = [];
}