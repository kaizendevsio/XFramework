﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class UpdateMedicineCmd : UpdateMedicineRequest, IRequest<CmdResponse<UpdateMedicineCmd>>
{
    
}