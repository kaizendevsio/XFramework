namespace XFramework.Client.Shared.Entity.Models.Requests.Session;

public class SignUpRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateTime? BindableBirthDate { get; set; }
    public DateOnly? BirthDate => BindableBirthDate is not null ? DateOnly.FromDateTime(BindableBirthDate.Value) : null;
    public short Gender { get; set; }
    public short CivilStatus { get; set; }
    public string? UserName { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
    public string? PasswordConfirmation { get; set; }
    public List<Guid>? RoleList { get; set; }
}