﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class VerifyDoctorIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}