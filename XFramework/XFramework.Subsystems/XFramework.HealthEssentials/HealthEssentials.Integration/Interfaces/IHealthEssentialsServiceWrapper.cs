using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace HealthEssentials.Integration.Interfaces;

public interface IHealthEssentialsServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<QueryResponse<IdentityValidationResponse>> VerifyDoctorIdentity(VerifyDoctorIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyIdentity(VerifyPharmacyIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryIdentity(VerifyLaboratoryIdentityRequest request);
}