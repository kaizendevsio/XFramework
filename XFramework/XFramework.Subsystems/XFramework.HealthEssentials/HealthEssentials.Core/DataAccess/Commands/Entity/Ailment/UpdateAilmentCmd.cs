﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class UpdateAilmentCmd : UpdateAilmentRequest, IRequest<CmdResponse<UpdateAilmentCmd>>
{
    
}