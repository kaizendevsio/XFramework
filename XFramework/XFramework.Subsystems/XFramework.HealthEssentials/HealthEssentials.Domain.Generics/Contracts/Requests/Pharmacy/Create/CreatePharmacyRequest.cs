﻿using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyRequest : RequestBase
{ 
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Slogan { get; set; }
    public string? Description { get; set; }
    public string? ChemicalComponent { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }
    public int Status { get; set; }
    public Guid? EntityGuid { get; set; }

    public List<FileUploadRequest> FileList { get; set; }
    public List<CreatePharmacyTagRequest> TagList { get; set; }
    public CreateAddressRequest? Address { get; set; }
}