using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Identity;

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