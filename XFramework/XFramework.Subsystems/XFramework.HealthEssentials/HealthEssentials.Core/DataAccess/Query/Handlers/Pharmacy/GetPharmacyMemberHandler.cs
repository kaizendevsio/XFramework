﻿using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyMemberHandler : QueryBaseHandler, IRequestHandler<GetPharmacyMemberQuery, QueryResponse<PharmacyMemberResponse>>
{
    public GetPharmacyMemberHandler()
    {
        
    }
    public async Task<QueryResponse<PharmacyMemberResponse>> Handle(GetPharmacyMemberQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}