using System;
using MemoryPack;
using XFramework.Domain.Shared.Contracts.Base;

namespace Inventario.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class Service : BaseModel
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public string? Description { get; set; }
    [MemoryPackOrder(2)]
    public decimal Price { get; set; }
    [MemoryPackOrder(3)]
    public int DurationInMinutes { get; set; }
    [MemoryPackOrder(4)]
    public string? Image { get; set; }
    [MemoryPackOrder(5)]
    public string? SKU { get; set; }
    [MemoryPackOrder(6)]
    public bool IsAvailable { get; set; }
}
