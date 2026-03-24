using MessagePack;
using XFramework.Domain.Shared.BusinessObjects;

namespace Bolt.Domain.Shared.BusinessObjects;

[MessagePackObject]
public class BoltQueue
{
    [Key(0)]
    public string Name { get; set; }
    
    [Key(1)]
    public Guid Id { get; set; }
}