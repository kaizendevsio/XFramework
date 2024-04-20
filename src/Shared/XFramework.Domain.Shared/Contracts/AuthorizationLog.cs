using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class AuthorizationLog : BaseModel
{
    public Guid CredentialId { get; set; }

    public string? Ipaddress { get; set; }

    public bool? IsSuccess { get; set; }

    public short? AuthStatus { get; set; }

    public string? LoginSource { get; set; }

    public string? DeviceName { get; set; }


    public virtual IdentityCredential IdentityCredentials { get; set; } = null!;
}