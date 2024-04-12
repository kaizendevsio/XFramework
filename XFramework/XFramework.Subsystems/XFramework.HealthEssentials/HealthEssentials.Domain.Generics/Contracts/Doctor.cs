using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Doctor : BaseModel, IHasOnlineStatus
{
    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }


    public string Name { get; set; } = null!;

    public int? ExperienceYears { get; set; }

    public string? Clinic { get; set; }

    public string? ClinicAddress { get; set; }

    public decimal? BaseFee { get; set; }

    public string? PrcNumber { get; set; }

    public string? PtrNumber { get; set; }

    public string? PhilHealthNumber { get; set; }

    public string? TinNumber { get; set; }

    public int Status { get; set; }

    public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; } =
        new List<DoctorConsultationJobOrder>();

    public virtual ICollection<DoctorConsultation> DoctorConsultations { get; set; } = new List<DoctorConsultation>();

    public virtual ICollection<DoctorTag> DoctorTags { get; set; } = new List<DoctorTag>();

    public virtual DoctorType Type { get; set; } = null!;

    [NotMapped]
    public IdentityCredential? IdentityCredential { get; set; }

    [NotMapped] 
    public ICollection<Wallet>? Wallets => IdentityCredential?.Wallets;
    
    [NotMapped]
    public List<StorageFile>? Files { get; set; }

    public bool IsOnline { get; set; }
    
    public DateTime LastSeen { get; set; }
    
    public DateTime? OnlineSince { get; set; }
    
    public string? StatusMessage { get; set; }
    
    public string? LastActivityType { get; set; }
    
    public string? Device { get; set; }
    
    public string? Location { get; set; }
}