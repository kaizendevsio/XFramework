﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class CreateDoctorConsultationCmd : CreateDoctorConsultationRequest, IRequest<CmdResponse<CreateDoctorConsultationRequest>>
{
    
}