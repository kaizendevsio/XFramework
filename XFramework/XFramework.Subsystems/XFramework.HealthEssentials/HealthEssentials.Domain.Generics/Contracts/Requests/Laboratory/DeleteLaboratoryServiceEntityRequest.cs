﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class DeleteLaboratoryServiceEntityRequest : RequestBase
{
    public Guid? Guid { get; set; }
}