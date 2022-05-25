using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;
using HealthEssentials.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;
using CreateLaboratoryServiceRequest = HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.CreateLaboratoryServiceRequest;

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
        return await SendAsync<VerifyDoctorIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyIdentity(VerifyPharmacyIdentityRequest request)
    {
        return await SendAsync<VerifyPharmacyIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPatientIdentity(VerifyPatientIdentityRequest request)
    {
        return await SendAsync<VerifyPatientIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request)
    {
        return await SendAsync<VerifyLogisticIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryIdentity(VerifyLaboratoryIdentityRequest request)
    {
        return await SendAsync<VerifyLaboratoryIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceList(GetLaboratoryServiceListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceListRequest, List<LaboratoryServiceEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationRequest>> CreateConsultationEntity(CreateConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationTypeGroupRequest>> CreateConsultationTypeGroup(CreateConsultationTypeGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationTypeRequest>> CreateConsultationType(CreateConsultationTypeRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateDoctorIdentityRequest>> CreateDoctorIdentity(CreateDoctorIdentityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryIdentityRequest>> CreateLaboratoryIdentity(CreateLaboratoryIdentityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceTypeGroupRequest>> CreateLaboratoryServiceTypeGroup(CreateLaboratoryServiceTypeGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceTypeRequest>> CreateLaboratoryServiceType(CreateLaboratoryServiceTypeRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreatePatientIdentityRequest>> CreatePatientIdentity(CreatePatientIdentityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreatePharmacyIdentityRequest>> CreatePharmacyIdentity(CreatePharmacyIdentityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationPaymentRequest>> CreateConsultationPayment(CreateConsultationPaymentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<List<ConsultationResponse>>> GetConsultationList(GetConsultationListRequest request)
    {
        return await SendAsync<GetConsultationListRequest, List<ConsultationResponse>>(request);
    }

    public async Task<QueryResponse<List<LaboratoryResponse>>> GetLaboratoryList(GetLaboratoryListRequest request)
    {
        return await SendAsync<GetLaboratoryListRequest, List<LaboratoryResponse>>(request);
    }

    public async Task<QueryResponse<List<LogisticResponse>>> GetLogisticList(GetLogisticListRequest request)
    {
        return await SendAsync<GetLogisticListRequest, List<LogisticResponse>>(request);
    }

    public async Task<QueryResponse<List<PharmacyResponse>>> GetPharmacyList(GetPharmacyListRequest request)
    {
        return await SendAsync<GetPharmacyListRequest, List<PharmacyResponse>>(request);
    }

    public async Task<QueryResponse<List<DoctorResponse>>> GetDoctorList(GetDoctorListRequest request)
    {
        return await SendAsync<GetDoctorListRequest, List<DoctorResponse>>(request);
    }

    public async Task<QueryResponse<PatientResponse>> GetPatientIdentity(GetPatientIdentityRequest request)
    {
        return await SendAsync<GetPatientIdentityRequest, PatientResponse>(request);
    }

    public async Task<QueryResponse<DoctorResponse>> GetDoctorIdentity(GetDoctorIdentityRequest request)
    {
        return await SendAsync<GetDoctorIdentityRequest, DoctorResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request)
    {
        return await SendAsync<GetSupportedConsultationListRequest, List<DoctorConsultationResponse>>(request);
    }
}   