namespace XFramework.Domain.Generic.Contracts;

public partial class AuthorizationLog
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public long IdentityCredentialsId { get; set; }

    public string? Ipaddress { get; set; }

    public bool? IsSuccess { get; set; }

    public short? AuthStatus { get; set; }

    public string? LoginSource { get; set; }

    public string? DeviceName { get; set; }

    
    public virtual IdentityCredential IdentityCredentials { get; set; } = null!;
}
