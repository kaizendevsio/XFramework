using IdentityServer.Domain.Generic.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetIdentityCredentialListRequest : RequestBase
{
    public RoleEntity IdentityRole { get; set; }
    public string SearchString { get; set; }
    public int ListCount { get; set; } = 50;
    public bool Filter { get; set; }
    public int Skip { get; set; }
}