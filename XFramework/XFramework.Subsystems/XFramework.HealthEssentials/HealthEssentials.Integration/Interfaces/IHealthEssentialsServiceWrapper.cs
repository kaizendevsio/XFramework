using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;
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
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPatientIdentity(VerifyPatientIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryIdentity(VerifyLaboratoryIdentityRequest request);
    
    public Task<CmdResponse<CreateConsultationRequest>> CreateConsultationEntity(CreateConsultationRequest request);
    public Task<CmdResponse<CreateConsultationTypeGroupRequest>> CreateConsultationTypeGroup(CreateConsultationTypeGroupRequest request);
    public Task<CmdResponse<CreateConsultationTypeRequest>> CreateConsultationType(CreateConsultationTypeRequest request);
    
    public Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request);
    public Task<CmdResponse<CreateDoctorIdentityRequest>> CreateDoctorIdentity(CreateDoctorIdentityRequest request);
    
    public Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request);
    public Task<CmdResponse<CreateLaboratoryIdentityRequest>> CreateLaboratoryIdentity(CreateLaboratoryIdentityRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceTypeGroupRequest>> CreateLaboratoryServiceTypeGroup(CreateLaboratoryServiceTypeGroupRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceTypeRequest>> CreateLaboratoryServiceType(CreateLaboratoryServiceTypeRequest request);
    
    public Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request);
    public Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request);
    public Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request);
    
    public Task<CmdResponse<CreatePatientIdentityRequest>> CreatePatientIdentity(CreatePatientIdentityRequest request);
    
    public Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request);
    public Task<CmdResponse<CreatePharmacyIdentityRequest>> CreatePharmacyIdentity(CreatePharmacyIdentityRequest request);
    
    
}