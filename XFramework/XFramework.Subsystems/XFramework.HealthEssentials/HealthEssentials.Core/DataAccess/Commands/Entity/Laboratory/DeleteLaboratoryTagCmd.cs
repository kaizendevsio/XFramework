﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class DeleteLaboratoryTagCmd : DeleteLaboratoryTagRequest, IRequest<CmdResponse<DeleteLaboratoryTagCmd>>
{
    
}