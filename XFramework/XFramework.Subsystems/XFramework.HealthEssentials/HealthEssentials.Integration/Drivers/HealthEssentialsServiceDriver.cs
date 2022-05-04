using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using HealthEssentials.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace HealthEssentials.Integration.Drivers;

public class HealthEssentialsServiceDriver : DriverBase, IHealthEssentialsServiceWrapper
{
    public HealthEssentialsServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:HealthEssentialsService"));
    }


    public async Task<QueryResponse<IdentityValidationResponse>> VerifyDoctorIdentity(VerifyDoctorIdentityRequest request)
    {
        return await SendAsync<VerifyDoctorIdentityRequest, IdentityValidationResponse>("VerifyDoctorIdentity", request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyIdentity(VerifyPharmacyIdentityRequest request)
    {
        return await SendAsync<VerifyPharmacyIdentityRequest, IdentityValidationResponse>("VerifyPharmacyIdentity", request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request)
    {
        return await SendAsync<VerifyLogisticIdentityRequest, IdentityValidationResponse>("VerifyLogisticIdentity", request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryIdentity(VerifyLaboratoryIdentityRequest request)
    {
        return await SendAsync<VerifyLaboratoryIdentityRequest, IdentityValidationResponse>("VerifyLaboratoryIdentity", request);
    }
}   