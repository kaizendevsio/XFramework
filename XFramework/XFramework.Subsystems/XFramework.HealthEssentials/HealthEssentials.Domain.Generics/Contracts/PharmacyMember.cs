﻿using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyMember : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid PharmacyId { get; set; }

    public string? Value { get; set; }


    public string Name { get; set; } = null!;

    public int Status { get; set; }

    public string? Description { get; set; }

    public Guid PharmacyBranchId { get; set; }

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual PharmacyBranch? PharmacyBranch { get; set; }
    
    [NotMapped]
    public IdentityCredential? Credential { get; set; }
}