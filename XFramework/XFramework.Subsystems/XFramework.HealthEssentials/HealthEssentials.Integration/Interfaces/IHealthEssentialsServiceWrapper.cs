using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Verify;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace HealthEssentials.Integration.Interfaces;

public interface IHealthEssentialsServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }

    #region Doctor Portal

    #region Doctor
    /// <summary>
    /// Validates the doctor login.
    /// </summary>
    public Task<QueryResponse<IdentityValidationResponse>> VerifyDoctor(VerifyDoctorRequest request);
    /// <summary>
    /// Gets the doctor profile.
    /// </summary>
    public Task<QueryResponse<DoctorResponse>> GetDoctor(GetDoctorRequest request);
    /// <summary>
    ///  Get all doctors in the system.
    /// </summary>
    public Task<QueryResponse<List<DoctorResponse>>> GetDoctorList(GetDoctorListRequest request);
    /// <summary>
    ///   Creates a new doctor in the system.
    /// </summary>
    public Task<CmdResponse<CreateDoctorRequest>> CreateDoctor(CreateDoctorRequest request);
    /// <summary>
    /// Updates the doctor profile.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorRequest>> UpdateDoctor(UpdateDoctorRequest request);
    /// <summary>
    /// Deletes the doctor from the system.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorRequest>> DeleteDoctor(DeleteDoctorRequest request);
    #endregion

    // No Supported Consultation in Database
    #region Supported Consultation
    public Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request);
    public Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request);
    #endregion
    
    #endregion

    #region Patient Portal
    /// <summary>
    /// Gets the patient profile.
    /// </summary>
    public Task<QueryResponse<PatientResponse>> GetPatient(GetPatientRequest request);
    /// <summary>
    /// Get all patients in the system.
    /// </summary>
    public Task<QueryResponse<List<PatientResponse>>> GetPatientList(GetPatientListRequest request);
    /// <summary>
    ///  Validates the patient login.
    /// </summary>
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPatient(VerifyPatientRequest request);
    /// <summary>
    ///  Creates a new patient in the system.
    /// </summary>
    public Task<CmdResponse<CreatePatientRequest>> CreatePatient(CreatePatientRequest request);
    /// <summary>
    ///  Updates the patient profile.
    /// </summary>
    public Task<CmdResponse<UpdatePatientRequest>> UpdatePatient(UpdatePatientRequest request);
    /// <summary>
    ///  Deletes the patient from the system.
    /// </summary>
    public Task<CmdResponse<DeletePatientRequest>> DeletePatient(DeletePatientRequest request);
    
    #endregion
    
    #region Pharmacy Portal

    #region Pharmacy
    /// <summary>
    ///  Gets the pharmacy profile.
    /// </summary>
    public Task<QueryResponse<PharmacyResponse>> GetPharmacy(GetPharmacyRequest request);
    /// <summary>
    ///  Get all pharmacies in the system.
    /// </summary>
    public Task<QueryResponse<List<PharmacyResponse>>> GetPharmacyList(GetPharmacyListRequest request);
    /// <summary>
    ///  Creates a new pharmacy in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request);
    /// <summary>
    ///  Updates the pharmacy profile.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyRequest>> UpdatePharmacy(UpdatePharmacyRequest request);
    /// <summary>
    ///  Deletes the pharmacy from the system.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyRequest>> DeletePharmacy(DeletePharmacyRequest request);
    #endregion
    
    #region Pharmacy Member
    
    /// <summary>
    /// Gets the pharmacy member profile.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public Task<QueryResponse<PharmacyMemberResponse>> GetPharmacyMember(GetPharmacyMemberRequest request);
    /// <summary>
    ///  Get all pharmacy members in the system.
    /// </summary>
    public Task<QueryResponse<List<PharmacyMemberResponse>>> GetPharmacyMemberList(GetPharmacyMemberListRequest request);
    /// <summary>
    /// Validates the pharmacy member login.
    /// </summary>
    public Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyMember(VerifyPharmacyMemberRequest request);
    /// <summary>
    ///  Creates a new pharmacy member in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyMemberRequest>> CreatePharmacyMember(CreatePharmacyMemberRequest request);
    /// <summary>
    ///  Updates the pharmacy member profile.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyMemberRequest>> UpdatePharmacyMember(UpdatePharmacyMemberRequest request);
    /// <summary>
    ///  Deletes the pharmacy member from the system.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyMemberRequest>> DeletePharmacyMember(DeletePharmacyMemberRequest request);
    
    #endregion
    
    #region Pharmacy Location

    /// <summary>
    /// Gets the pharmacy location details.
    /// </summary>
    public Task<QueryResponse<PharmacyLocationResponse>> GetPharmacyLocation(GetPharmacyLocationRequest request);
    /// <summary>
    ///  Get all pharmacy locations in the system.
    /// </summary>
    public Task<QueryResponse<List<PharmacyLocationResponse>>> GetPharmacyLocationList(GetPharmacyLocationListRequest request);
    /// <summary>
    ///  Creates a new pharmacy location in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyLocationRequest>> CreatePharmacyLocation(CreatePharmacyLocationRequest request);
    /// <summary>
    ///  Updates the pharmacy location details.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyLocationRequest>> UpdatePharmacyLocation(UpdatePharmacyLocationRequest request);
    /// <summary>
    ///  Deletes the pharmacy location from the system.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyLocationRequest>> DeletePharmacyLocation(DeletePharmacyLocationRequest request);
    

    #endregion
    
    #region Pharmacy Location Document

    /// <summary>
    /// Gets the pharmacy location document details.
    /// </summary>
    public Task<QueryResponse<StorageFileResponse>> GetPharmacyLocationDocument(GetPharmacyLocationDocumentRequest request);
    /// <summary>
    ///  Get all pharmacy location documents in the system.
    /// </summary>
    public Task<QueryResponse<List<StorageFileResponse>>> GetPharmacyLocationDocumentList(GetPharmacyLocationDocumentListRequest request);
    /// <summary>
    ///  Creates a new pharmacy location document in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyLocationDocumentRequest>> CreatePharmacyLocationDocument(CreatePharmacyLocationDocumentRequest request);
    /// <summary>
    ///  Updates the pharmacy location document details.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyLocationDocumentRequest>> UpdatePharmacyLocationDocument(UpdatePharmacyLocationDocumentRequest request);
    /// <summary>
    ///  Deletes the pharmacy location document from the system.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyLocationDocumentRequest>> DeletePharmacyLocationDocument(DeletePharmacyLocationDocumentRequest request);
    

    #endregion
    
    #endregion

    #region Logistic Portal

    #region Logistic
    /// <summary>
    /// Gets the logistic profile.
    /// </summary>
    public Task<QueryResponse<LogisticResponse>> GetLogistic(GetLogisticRequest request);
    /// <summary>
    /// Get all logistics in the system.
    /// </summary>
    public Task<QueryResponse<List<LogisticResponse>>> GetLogisticList(GetLogisticListRequest request);
    /// <summary>
    ///  Creates a new logistic in the system.
    /// </summary>
    public Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request);
    /// <summary>
    /// Updates the logistic profile.
    /// </summary>
    public Task<CmdResponse<UpdateLogisticRequest>> UpdateLogistic(UpdateLogisticRequest request);
    /// <summary>
    ///  Deletes the logistic from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLogisticRequest>> DeleteLogistic(DeleteLogisticRequest request);
    #endregion

    #region Logistic Rider Handle
    /// <summary>
    ///  Gets the logistic rider handle profile.
    /// </summary>
    public Task<QueryResponse<LogisticRiderHandleResponse>> GetLogisticRiderHandle(GetLogisticRiderHandleRequest request);
    /// <summary>
    ///  Get all logistic rider handles in the system.
    /// </summary>
    public Task<QueryResponse<List<LogisticRiderHandleResponse>>> GetLogisticRiderHandleList(GetLogisticRiderHandleListRequest request);
    /// <summary>
    ///  Creates a new logistic rider handle in the system.
    /// </summary>
    public Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request);
    /// <summary>
    ///  Updates the logistic rider handle profile.
    /// </summary>
    public Task<CmdResponse<UpdateLogisticRiderHandleRequest>> UpdateLogisticRiderHandle(UpdateLogisticRiderHandleRequest request);
    /// <summary>
    ///  Deletes the logistic rider handle from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLogisticRiderHandleRequest>> DeleteLogisticRiderHandle(DeleteLogisticRiderHandleRequest request);
    
    #endregion

    #region Logistic Rider
    /// <summary>
    /// Gets the logistic rider profile.
    /// </summary>
    public Task<QueryResponse<LogisticRiderResponse>> GetLogisticRider(GetLogisticRiderRequest request);
    /// <summary>
    /// Get all logistic riders in the system.
    /// </summary>
    public Task<QueryResponse<List<LogisticRiderResponse>>> GetLogisticRiderList(GetLogisticRiderListRequest request);
    /// <summary>
    /// Validates the logistic rider login.
    /// </summary>
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticRider(VerifyLogisticRiderRequest request);
    /// <summary>
    ///  Creates a new logistic rider in the system.
    /// </summary>
    public Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request);
    /// <summary>
    ///  Updates the logistic rider profile.
    /// </summary>
    public Task<CmdResponse<UpdateLogisticRiderRequest>> UpdateLogisticRider(UpdateLogisticRiderRequest request);
    /// <summary>
    ///  Deletes the logistic rider from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLogisticRiderRequest>> DeleteLogisticRider(DeleteLogisticRiderRequest request);
    #endregion
    
    #endregion

    #region Laboratory Portal
    
    #region Laboratory
    /// <summary>
    /// Gets the laboratory profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryResponse>> GetLaboratory(GetLaboratoryRequest request);
    /// <summary>
    ///  Get all laboratories in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryResponse>>> GetLaboratoryList(GetLaboratoryListRequest request);
    /// <summary>
    ///  Creates a new laboratory in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request);
    /// <summary>
    ///  Updates the laboratory profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryRequest>> UpdateLaboratory(UpdateLaboratoryRequest request);
    /// <summary>
    ///  Deletes the laboratory from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryRequest>> DeleteLaboratory(DeleteLaboratoryRequest request);
    #endregion

    #region Laboratory Location

    /// <summary>
    /// Gets the laboratory location details.
    /// </summary>
    public Task<QueryResponse<LaboratoryLocationResponse>> GetLaboratoryLocation(GetLaboratoryLocationRequest request);
    /// <summary>
    ///  Get all laboratory locations in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryLocationResponse>>> GetLaboratoryLocationList(GetLaboratoryLocationListRequest request);
    /// <summary>
    ///  Creates a new laboratory location in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryLocationRequest>> CreateLaboratoryLocation(CreateLaboratoryLocationRequest request);
    /// <summary>
    ///  Updates the laboratory location profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryLocationRequest>> UpdateLaboratoryLocation(UpdateLaboratoryLocationRequest request);
    /// <summary>
    ///  Deletes the laboratory location from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryLocationRequest>> DeleteLaboratoryLocation(DeleteLaboratoryLocationRequest request);
    

    #endregion
    
    #region Laboratory Location Document

    /// <summary>
    /// Gets the laboratory location document details.
    /// </summary>
    public Task<QueryResponse<StorageFileResponse>> GetLaboratoryLocationDocument(GetLaboratoryLocationDocumentRequest request);
    /// <summary>
    ///  Get all laboratory location documents in the system.
    /// </summary>
    public Task<QueryResponse<List<StorageFileResponse>>> GetLaboratoryLocationDocumentList(GetLaboratoryLocationDocumentListRequest request);
    /// <summary>
    ///  Creates a new laboratory location document in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryLocationDocumentRequest>> CreateLaboratoryLocationDocument(CreateLaboratoryLocationDocumentRequest request);
    /// <summary>
    ///  Updates the laboratory location document profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryLocationDocumentRequest>> UpdateLaboratoryLocationDocument(UpdateLaboratoryLocationDocumentRequest request);
    /// <summary>
    ///  Deletes the laboratory location document from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryLocationDocumentRequest>> DeleteLaboratoryLocationDocument(DeleteLaboratoryLocationDocumentRequest request);
    

    #endregion
    
    #region Laboratory Job Order

    /// <summary>
    /// Gets the laboratory job order details.
    /// </summary>
    public Task<QueryResponse<LaboratoryJobOrderResponse>> GetLaboratoryJobOrder(GetLaboratoryJobOrderRequest request);
    /// <summary>
    ///  Get all laboratory job orders in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryJobOrderResponse>>> GetLaboratoryJobOrderList(GetLaboratoryJobOrderListRequest request);
    /// <summary>
    ///  Creates a new laboratory job order in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryJobOrderRequest>> CreateLaboratoryJobOrder(CreateLaboratoryJobOrderRequest request);
    /// <summary>
    ///  Updates the laboratory job order profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryJobOrderRequest>> UpdateLaboratoryJobOrder(UpdateLaboratoryJobOrderRequest request);
    /// <summary>
    ///  Deletes the laboratory job order from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryJobOrderRequest>> DeleteLaboratoryJobOrder(DeleteLaboratoryJobOrderRequest request);
    

    #endregion

    #region Laboratory Member
    /// <summary>
    ///  Gets the laboratory member profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryMemberResponse>> GetLaboratoryMember(GetLaboratoryMemberRequest request);
    /// <summary>
    ///  Get all laboratory members in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryMemberResponse>>> GetLaboratoryMemberList(GetLaboratoryMemberListRequest request);
    /// <summary>
    /// Validates the laboratory member login.
    /// </summary>
    public Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryMember(VerifyLaboratoryMemberRequest request);
    /// <summary>
    /// Creates a new laboratory member in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryMemberRequest>> CreateLaboratoryMember(CreateLaboratoryMemberRequest request);
    /// <summary>
    ///  Updates the laboratory member profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryMemberRequest>> UpdateLaboratoryMember(UpdateLaboratoryMemberRequest request);
    /// <summary>
    ///  Deletes the laboratory member from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryMemberRequest>> DeleteLaboratoryMember(DeleteLaboratoryMemberRequest request);
    
    #endregion

    #region Laboratory Service
    /// <summary>
    ///  Gets the laboratory service profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryServiceResponse>> GetLaboratoryService(GetLaboratoryServiceRequest request);
    /// <summary>
    ///  Get all laboratory services in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryServiceResponse>>> GetLaboratoryServiceList(GetLaboratoryServiceListRequest request);
    /// <summary>
    ///  Creates a new laboratory service in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request);
    /// <summary>
    ///  Updates the laboratory service profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryServiceRequest>> UpdateLaboratoryService(UpdateLaboratoryServiceRequest request);
    /// <summary>
    ///  Deletes the laboratory service from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryServiceRequest>> DeleteLaboratoryService(DeleteLaboratoryServiceRequest request);
    
    #endregion
    
    #region Laboratory Service Entity
    
    /// <summary>
    ///  Gets the laboratory service entity profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryServiceEntityResponse>> GetLaboratoryServiceEntity(GetLaboratoryServiceEntityRequest request);
    /// <summary>
    ///  Get all laboratory service entities in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceEntityList(GetLaboratoryServiceEntityListRequest request);
    /// <summary>
    ///  Creates a new laboratory service entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryServiceEntityRequest>> CreateLaboratoryServiceEntity(CreateLaboratoryServiceEntityRequest request);
    /// <summary>
    ///  Updates the laboratory service entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryServiceEntityRequest>> UpdateLaboratoryServiceEntity(UpdateLaboratoryServiceEntityRequest request);
    /// <summary>
    ///  Deletes the laboratory service entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryServiceEntityRequest>> DeleteLaboratoryServiceEntity(DeleteLaboratoryServiceEntityRequest request);

    #endregion

    #region Laboratory Service Entity Group
    /// <summary>
    ///  Gets the laboratory service entity group profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryServiceEntityGroupResponse>> GetLaboratoryServiceEntityGroup(GetLaboratoryServiceEntityGroupRequest request);
    /// <summary>
    ///  Get all laboratory service entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> GetLaboratoryServiceEntityGroupList(GetLaboratoryServiceEntityGroupListRequest request);
    /// <summary>
    ///  Creates a new laboratory service entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryServiceEntityGroupRequest>> CreateLaboratoryServiceEntityGroup(CreateLaboratoryServiceEntityGroupRequest request);
    /// <summary>
    ///  Updates the laboratory service entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryServiceEntityGroupRequest>> UpdateLaboratoryServiceEntityGroup(UpdateLaboratoryServiceEntityGroupRequest request);
    /// <summary>
    ///  Deletes the laboratory service entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryServiceEntityGroupRequest>> DeleteLaboratoryServiceEntityGroup(DeleteLaboratoryServiceEntityGroupRequest request);
    #endregion
    
    #endregion
    
    #region Consultation Portal
    #region Consultation Entity
    /// <summary>
    ///  Gets the consultation entity profile.
    /// </summary>
    public Task<QueryResponse<ConsultationEntityResponse>> GetConsultationEntity(GetConsultationEntityRequest request);
    /// <summary>
    ///  Get all consultation entities in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationEntityResponse>>> GetConsultationEntityList(GetConsultationEntityListRequest request);
    /// <summary>
    ///  Creates a new consultation entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationEntityRequest>> CreateConsultationEntity(CreateConsultationEntityRequest request);
    /// <summary>
    ///  Updates the consultation entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationEntityRequest>> UpdateConsultationEntity(UpdateConsultationEntityRequest request);
    /// <summary>
    ///  Deletes the consultation entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationEntityRequest>> DeleteConsultationEntity(DeleteConsultationEntityRequest request);
    
    #endregion

    #region Consultation Payment
    /// <summary>
    ///  Gets the consultation payment profile.
    /// </summary>
    public Task<QueryResponse<ConsultationPaymentResponse>> GetConsultationPayment(GetConsultationPaymentRequest request);
    /// <summary>
    ///  Get all consultation payments in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationPaymentResponse>>> GetConsultationPaymentList(GetConsultationPaymentListRequest request);
    /// <summary>
    ///  Creates a new consultation payment in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationPaymentRequest>> CreateConsultationPayment(CreateConsultationPaymentRequest request);
    /// <summary>
    ///  Updates the consultation payment profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationPaymentRequest>> UpdateConsultationPayment(UpdateConsultationPaymentRequest request);
    /// <summary>
    ///  Deletes the consultation payment from the system.
    /// </summary>
    public Task<CmdResponse> DeleteConsultationPayment(DeleteConsultationPaymentRequest request);

    #endregion

    #region Consultation
    /// <summary>
    ///  Gets the consultation profile.
    /// </summary>
    public Task<QueryResponse<ConsultationResponse>> GetConsultation(GetConsultationRequest request);
    /// <summary>
    ///  Get all consultations in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationResponse>>> GetConsultationList(GetConsultationListRequest request);
    /// <summary>
    ///  Creates a new consultation in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationRequest>> CreateConsultation(CreateConsultationRequest request);
    /// <summary>
    ///  Updates the consultation profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationRequest>> UpdateConsultation(UpdateConsultationRequest request);
    /// <summary>
    ///  Deletes the consultation from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationRequest>> DeleteConsultation(DeleteConsultationRequest request);
    #endregion
    #endregion

    #region Administrator Portal
    
    #region Member
    public Task<QueryResponse<List<CredentialResponse>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request);

    #endregion

    #endregion
    
}