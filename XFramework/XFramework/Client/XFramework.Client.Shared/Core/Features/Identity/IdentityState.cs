using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState : State<IdentityState>
{
    public override void Initialize()
    {
    }

    public IdentityCredential? Credential { get; set; }
    public IdentityInformation? Identity { get; set; }
    public List<IdentityAddress>? Addresses { get; set; }
    public List<IdentityRole>? Roles { get; set; }
    public List<IdentityContact>? Contacts { get; set; }
    public IdentityContact? SelectedContact { get; set; }
    public PaginatedResult<IdentityCredential>? CredentialList { get; set; }
}