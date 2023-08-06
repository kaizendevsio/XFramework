namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityContactGroup
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();
}
