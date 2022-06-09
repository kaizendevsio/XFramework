using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor;
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
using IdentityServer.Domain.Generic.Contracts.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace HealthEssentials.Integration.Interfaces;

public interface IHealthEssentialsServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }

    #region Administrator
    public Task<QueryResponse<List<CredentialResponse>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request);

    #endregion
    
    #region Doctor

    public Task<CmdResponse<CreateDoctorIdentityRequest>> CreateDoctorIdentity(CreateDoctorIdentityRequest request);
    public Task<CmdResponse<UpdateDoctorIdentityRequest>> UpdateDoctorIdentity(UpdateDoctorIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyDoctorIdentity(VerifyDoctorIdentityRequest request);
    public Task<QueryResponse<DoctorResponse>> GetDoctorIdentity(GetDoctorIdentityRequest request);
    public Task<QueryResponse<List<DoctorResponse>>> GetDoctorList(GetDoctorListRequest request);
    public Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request);
    public Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request);
    
    #endregion

    #region Patient
    
    public Task<CmdResponse<CreatePatientIdentityRequest>> CreatePatientIdentity(CreatePatientIdentityRequest request);
    public Task<QueryResponse<PatientResponse>> GetPatientIdentity(GetPatientIdentityRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPatientIdentity(VerifyPatientIdentityRequest request);
    
    #endregion
    
    #region Pharmacy
    
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyIdentity(VerifyPharmacyIdentityRequest request);
    public Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request);
    public Task<CmdResponse<UpdatePharmacyRequest>> UpdatePharmacy(UpdatePharmacyRequest request);
    public Task<CmdResponse<UpdatePharmacyMemberRequest>> UpdatePharmacyMember(UpdatePharmacyMemberRequest request);
    public Task<CmdResponse<CreatePharmacyIdentityRequest>> CreatePharmacyIdentity(CreatePharmacyIdentityRequest request);
    public Task<QueryResponse<List<PharmacyResponse>>> GetPharmacyList(GetPharmacyListRequest request);
    
    #endregion
    
    #region Logistic
    
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request);
    public Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request);
    public Task<CmdResponse<UpdateLogisticRiderRequest>> UpdatePharmacyMember(UpdateLogisticRiderRequest request);
    public Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request);
    public Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request);
    public Task<QueryResponse<List<LogisticResponse>>> GetLogisticList(GetLogisticListRequest request);
    
    #endregion
    
    #region Laboratory
    
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryIdentity(VerifyLaboratoryIdentityRequest request);
    public Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request);
    public Task<CmdResponse<UpdateLaboratoryRequest>> UpdateLaboratory(UpdateLaboratoryRequest request);
    public Task<CmdResponse<UpdateLaboratoryMemberRequest>> UpdateLaboratoryMember(UpdateLaboratoryMemberRequest request);
    public Task<CmdResponse<CreateLaboratoryIdentityRequest>> CreateLaboratoryIdentity(CreateLaboratoryIdentityRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceTypeGroupRequest>> CreateLaboratoryServiceTypeGroup(CreateLaboratoryServiceTypeGroupRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceTypeRequest>> CreateLaboratoryServiceType(CreateLaboratoryServiceTypeRequest request);
    public Task<QueryResponse<List<LaboratoryResponse>>> GetLaboratoryList(GetLaboratoryListRequest request);
    public Task<QueryResponse<LaboratoryResponse>> GetLaboratory(GetLaboratoryRequest request);
    public Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceList(GetLaboratoryServiceListRequest request);
    
    #endregion
    
    #region Consultation
    
    public Task<CmdResponse<CreateConsultationRequest>> CreateConsultationEntity(CreateConsultationRequest request);
    public Task<CmdResponse<CreateConsultationTypeGroupRequest>> CreateConsultationTypeGroup(CreateConsultationTypeGroupRequest request);
    public Task<CmdResponse<CreateConsultationTypeRequest>> CreateConsultationType(CreateConsultationTypeRequest request);
    public Task<CmdResponse<CreateConsultationPaymentRequest>> CreateConsultationPayment(CreateConsultationPaymentRequest request);
    public Task<QueryResponse<List<ConsultationResponse>>> GetConsultationList(GetConsultationListRequest request);
    
    #endregion
    
    #region Common
    
    #endregion
    
}