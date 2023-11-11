namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityContactGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();
}