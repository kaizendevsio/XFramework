﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Doctor;

public class GetDoctorEntityGroupListQuery : GetDoctorEntityGroupListRequest, IRequest<QueryResponse<List<DoctorEntityGroupResponse>>>
{
    
}