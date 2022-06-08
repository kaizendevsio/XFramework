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
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace HealthEssentials.Integration.Interfaces;

public interface IHealthEssentialsServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }

    #region Doctor Portal

    #region Doctor
    public Task<QueryResponse<IdentityValidationResponse>> VerifyDoctor(VerifyDoctorRequest request);
    public Task<QueryResponse<DoctorResponse>> GetDoctor(GetDoctorRequest request);
    public Task<QueryResponse<List<DoctorResponse>>> GetDoctorList(GetDoctorListRequest request);
    public Task<CmdResponse<CreateDoctorRequest>> CreateDoctor(CreateDoctorRequest request);
    public Task<CmdResponse<UpdateDoctorRequest>> UpdateDoctor(UpdateDoctorRequest request);
    public Task<CmdResponse<DeleteDoctorRequest>> DeleteDoctor(DeleteDoctorRequest request);
    #endregion

    // No Supported Consultation in Database
    #region Supported Consultation
    public Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request);
    public Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request);
    #endregion
    
    #endregion

    #region Patient
    public Task<QueryResponse<PatientResponse>> GetPatient(GetPatientRequest request);
    public Task<QueryResponse<List<PatientResponse>>> GetPatientList(GetPatientListRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPatient(VerifyPatientRequest request);
    public Task<CmdResponse<CreatePatientRequest>> CreatePatient(CreatePatientRequest request);
    public Task<CmdResponse<UpdatePatientRequest>> UpdatePatient(UpdatePatientRequest request);
    public Task<CmdResponse<DeletePatientRequest>> DeletePatient(DeletePatientRequest request);
    
    #endregion
    
    #region Pharmacy Portal

    #region Pharmacy
    public Task<QueryResponse<List<PharmacyResponse>>> GetPharmacyList(GetPharmacyListRequest request);
    public Task<QueryResponse<PharmacyResponse>> GetPharmacy(GetPharmacyRequest request);
    public Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request);
    public Task<CmdResponse<UpdatePharmacyRequest>> UpdatePharmacy(UpdatePharmacyRequest request);
    public Task<CmdResponse<DeletePharmacyRequest>> DeletePharmacy(DeletePharmacyRequest request);
    #endregion
    
    // Pharmacy Identity -> Pharmacy Member
    #region Pharmacy Member
    
    public Task<QueryResponse<PharmacyMemberResponse>> GetPharmacyMember(GetPharmacyMemberRequest request);
    public Task<QueryResponse<List<PharmacyMemberResponse>>> GetPharmacyMemberList(GetPharmacyMemberListRequest request);
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyMember(VerifyPharmacyMemberRequest request);
    public Task<CmdResponse<CreatePharmacyMemberRequest>> CreatePharmacyMember(CreatePharmacyMemberRequest request);
    public Task<CmdResponse<UpdatePharmacyMemberRequest>> UpdatePharmacyMember(UpdatePharmacyMemberRequest request);
    public Task<CmdResponse<DeletePharmacyMemberRequest>> DeletePharmacyMember(DeletePharmacyMemberRequest request);
    
    #endregion
    
    #endregion

    #region Logistic Portal

    #region Logistic
    public Task<QueryResponse<List<LogisticResponse>>> GetLogisticList(GetLogisticListRequest request);
    public Task<QueryResponse<LogisticResponse>> GetLogistic(GetLogisticRequest request);
    public Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request);
    public Task<CmdResponse<UpdateLogisticRequest>> UpdateLogistic(UpdateLogisticRequest request);
    public Task<CmdResponse<DeleteLogisticRequest>> DeleteLogistic(DeleteLogisticRequest request);
    #endregion
    
    // Logistic Identity not in database
    /*#region Logistic Identity
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request);
    #endregion*/
    
    #region Logistic Rider Handle
    public Task<QueryResponse<LogisticRiderHandleResponse>> GetLogisticRiderHandle(GetLogisticRiderHandleRequest request);
    public Task<QueryResponse<List<LogisticRiderHandleResponse>>> GetLogisticRiderHandleList(GetLogisticRiderHandleListRequest request);
    public Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request);
    public Task<CmdResponse<UpdateLogisticRiderHandleRequest>> UpdateLogisticRiderHandle(UpdateLogisticRiderHandleRequest request);
    public Task<CmdResponse<DeleteLogisticRiderHandleRequest>> DeleteLogisticRiderHandle(DeleteLogisticRiderHandleRequest request);
    
    #endregion

    #region Logistic Rider
    public Task<QueryResponse<LogisticRiderResponse>> GetLogisticRider(GetLogisticRiderRequest request);
    public Task<QueryResponse<List<LogisticRiderResponse>>> GetLogisticRiderList(GetLogisticRiderListRequest request);
    public Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request);
    public Task<CmdResponse<UpdateLogisticRiderRequest>> UpdateLogisticRider(UpdateLogisticRiderRequest request);
    public Task<CmdResponse<DeleteLogisticRiderRequest>> DeleteLogisticRider(DeleteLogisticRiderRequest request);
    #endregion
    
    #endregion
    
    
    #region Laboratory Portal
    #region Laboratory
    public Task<QueryResponse<LaboratoryResponse>> GetLaboratory(GetLaboratoryRequest request);
    public Task<QueryResponse<List<LaboratoryResponse>>> GetLaboratoryList(GetLaboratoryListRequest request);
    public Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request);
    public Task<CmdResponse<UpdateLaboratoryRequest>> UpdateLaboratory(UpdateLaboratoryRequest request);
    public Task<CmdResponse<DeleteLaboratoryRequest>> DeleteLaboratory(DeleteLaboratoryRequest request);
    #endregion

    // Laboratory Identity -> Laboratory Member
    #region Laboratory Member
    public Task<QueryResponse<LaboratoryMemberResponse>> GetLaboratoryMember(GetLaboratoryMemberRequest request);
    public Task<QueryResponse<List<LaboratoryMemberResponse>>> GetLaboratoryMemberList(GetLaboratoryMemberListRequest request);
    
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryMember(VerifyLaboratoryMemberRequest request);
    public Task<CmdResponse<CreateLaboratoryMemberRequest>> CreateLaboratoryMember(CreateLaboratoryMemberRequest request);
    public Task<CmdResponse<UpdateLaboratoryMemberRequest>> UpdateLaboratoryMember(UpdateLaboratoryMemberRequest request);
    public Task<CmdResponse<DeleteLaboratoryMemberRequest>> DeleteLaboratoryMember(DeleteLaboratoryMemberRequest request);
    
    #endregion

    #region Laboratory Service

    public Task<QueryResponse<List<LaboratoryServiceResponse>>> GetLaboratoryServiceList(GetLaboratoryServiceListRequest request);
    public Task<QueryResponse<LaboratoryServiceResponse>> GetLaboratoryService(GetLaboratoryServiceRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request);
    public Task<CmdResponse<UpdateLaboratoryServiceRequest>> UpdateLaboratoryService(UpdateLaboratoryServiceRequest request);
    public Task<CmdResponse<DeleteLaboratoryServiceRequest>> DeleteLaboratoryService(DeleteLaboratoryServiceRequest request);
    
    #endregion
    
    #region Laboratory Service Entity
    
    public Task<QueryResponse<LaboratoryServiceEntityResponse>> GetLaboratoryServiceEntity(GetLaboratoryServiceEntityRequest request);
    public Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceEntityList(GetLaboratoryServiceEntityListRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceEntityRequest>> CreateLaboratoryServiceEntity(CreateLaboratoryServiceEntityRequest request);
    public Task<CmdResponse<UpdateLaboratoryServiceEntityRequest>> UpdateLaboratoryServiceEntity(UpdateLaboratoryServiceEntityRequest request);
    public Task<CmdResponse<DeleteLaboratoryServiceEntityRequest>> DeleteLaboratoryServiceEntity(DeleteLaboratoryServiceEntityRequest request);

    #endregion

    #region Laboratory Service Entity Group
    public Task<QueryResponse<LaboratoryServiceEntityGroupResponse>> GetLaboratoryServiceEntityGroup(GetLaboratoryServiceEntityGroupRequest request);
    public Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> GetLaboratoryServiceEntityGroupList(GetLaboratoryServiceEntityGroupListRequest request);
    public Task<CmdResponse<CreateLaboratoryServiceEntityGroupRequest>> CreateLaboratoryServiceEntityGroup(CreateLaboratoryServiceEntityGroupRequest request);
    public Task<CmdResponse<UpdateLaboratoryServiceEntityGroupRequest>> UpdateLaboratoryServiceEntityGroup(UpdateLaboratoryServiceEntityGroupRequest request);
    public Task<CmdResponse<DeleteLaboratoryServiceEntityGroupRequest>> DeleteLaboratoryServiceEntityGroup(DeleteLaboratoryServiceEntityGroupRequest request);
    #endregion
    #endregion
    
    #region Consultation Portal
    #region Consultation Entity
    
    public Task<QueryResponse<List<ConsultationEntityResponse>>> GetConsultationEntityList(GetConsultationEntityListRequest request);
    public Task<QueryResponse<ConsultationEntityResponse>> GetConsultationEntity(GetConsultationEntityRequest request);
    public Task<CmdResponse<CreateConsultationEntityRequest>> CreateConsultationEntity(CreateConsultationEntityRequest request);
    public Task<CmdResponse<UpdateConsultationEntityRequest>> UpdateConsultationEntity(UpdateConsultationEntityRequest request);
    public Task<CmdResponse<DeleteConsultationEntityRequest>> DeleteConsultationEntity(DeleteConsultationEntityRequest request);
    
    #endregion

    #region Consultation Payment
    public Task<QueryResponse<ConsultationPaymentResponse>> GetConsultationPayment(GetConsultationPaymentRequest request);
    public Task<QueryResponse<List<ConsultationPaymentResponse>>> GetConsultationPaymentList(GetConsultationPaymentListRequest request);
    public Task<CmdResponse<CreateConsultationPaymentRequest>> CreateConsultationPayment(CreateConsultationPaymentRequest request);

    public Task<CmdResponse<UpdateConsultationPaymentRequest>> UpdateConsultationPayment(UpdateConsultationPaymentRequest request);
    public Task<CmdResponse> DeleteConsultationPayment(DeleteConsultationPaymentRequest request);

    #endregion

    #region Consultation
    public Task<QueryResponse<List<ConsultationResponse>>> GetConsultationList(GetConsultationListRequest request);
    public Task<QueryResponse<ConsultationResponse>> GetConsultation(GetConsultationRequest request);
    public Task<CmdResponse<CreateConsultationRequest>> CreateConsultation(CreateConsultationRequest request);
    public Task<CmdResponse<UpdateConsultationRequest>> UpdateConsultation(UpdateConsultationRequest request);
    public Task<CmdResponse> DeleteConsultation(DeleteConsultationRequest request);
    #endregion
    #endregion
    
    #region Common
    
    #endregion
    
}