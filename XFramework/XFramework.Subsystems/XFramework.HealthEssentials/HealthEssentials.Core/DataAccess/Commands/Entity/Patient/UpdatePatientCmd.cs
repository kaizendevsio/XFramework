﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class UpdatePatientCmd : UpdatePatientRequest, IRequest<CmdResponse<UpdatePatientCmd>>
{
    
}