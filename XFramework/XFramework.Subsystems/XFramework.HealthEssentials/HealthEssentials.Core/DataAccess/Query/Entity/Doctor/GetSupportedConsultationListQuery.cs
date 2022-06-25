﻿using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Doctor;

public class GetSupportedConsultationListQuery : GetSupportedConsultationListRequest, IRequest<QueryResponse<List<DoctorConsultationResponse>>>
{
    
}