﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class DeleteLaboratoryServiceEntityGroupRequest : RequestBase
{
    public Guid? Guid { get; set; }
}