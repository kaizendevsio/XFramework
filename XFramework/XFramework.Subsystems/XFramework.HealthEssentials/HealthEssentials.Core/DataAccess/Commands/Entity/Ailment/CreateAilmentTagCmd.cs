﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class CreateAilmentTagCmd : CreateAilmentTagRequest, IRequest<CmdResponse<CreateAilmentTagCmd>>
{
    
}