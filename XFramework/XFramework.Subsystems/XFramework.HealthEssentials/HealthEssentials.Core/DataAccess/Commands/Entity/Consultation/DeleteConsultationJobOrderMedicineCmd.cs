﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class DeleteConsultationJobOrderMedicineCmd : DeleteConsultationJobOrderMedicineRequest, IRequest<CmdResponse<DeleteConsultationJobOrderMedicineCmd>>
{
    
}