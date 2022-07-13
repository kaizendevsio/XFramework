﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyListRequest : QueryableRequest
{
    public GenericStatusType Status { get; set; }
}