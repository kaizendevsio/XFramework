namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateIdentityRequest : RequestBase,
    ICommand<CmdResponse<IdentityAdministrationResponse>>,
    IBoltRequest<CreateIdentityRequest, CmdResponse<IdentityAdministrationResponse>>
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public CivilStatus? CivilStatus { get; set; }
}

[MemoryPackable]
public partial record UpdateIdentityProfileRequest : RequestBase,
    ICommand<CmdResponse<IdentityAdministrationResponse>>,
    IBoltRequest<UpdateIdentityProfileRequest, CmdResponse<IdentityAdministrationResponse>>
{
    public Guid IdentityId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public CivilStatus? CivilStatus { get; set; }
}

[MemoryPackable]
public partial record SetIdentityEnabledRequest : RequestBase,
    ICommand<CmdResponse<IdentityAdministrationResponse>>,
    IBoltRequest<SetIdentityEnabledRequest, CmdResponse<IdentityAdministrationResponse>>
{
    public Guid IdentityId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
    public bool IsEnabled { get; set; }
}

[MemoryPackable]
public partial record SoftDeleteIdentityRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<SoftDeleteIdentityRequest, CmdResponse>
{
    public Guid IdentityId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
}
