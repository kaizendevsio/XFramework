namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AuthorizationLog : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? Ipaddress { get; set; }

    [MemoryPackOrder(2)]
    public bool? IsSuccess { get; set; }

    [MemoryPackOrder(3)]
    public short? AuthStatus { get; set; }

    [MemoryPackOrder(4)]
    public string? LoginSource { get; set; }

    [MemoryPackOrder(5)]
    public string? DeviceName { get; set; }


    [MemoryPackOrder(6)]
    public virtual IdentityCredential IdentityCredentials { get; set; } = null!;
}
