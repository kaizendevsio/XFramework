using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;
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
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;
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
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Update;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;
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
    #region Doctor Consultation
    /// <summary>
    /// Gets the doctor consultation.
    /// </summary>
    public Task<QueryResponse<DoctorConsultationResponse>> GetDoctorConsultation(GetDoctorConsultationRequest request);
    /// <summary>
    /// Gets the doctor consultation list.
    /// </summary>
    public Task<QueryResponse<List<DoctorConsultationResponse>>> GetDoctorConsultationList(GetDoctorConsultationListRequest request);
    /// <summary>
    ///  Creates a new doctor consultation.
    /// </summary>
    public Task<CmdResponse<CreateDoctorConsultationRequest>> CreateDoctorConsultation(CreateDoctorConsultationRequest request);
    /// <summary>
    /// Updates the doctor consultation.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorConsultationRequest>> UpdateDoctorConsultation(UpdateDoctorConsultationRequest request);
    /// <summary>
    /// Deletes the doctor consultation.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorConsultationRequest>> DeleteDoctorConsultation(DeleteDoctorConsultationRequest request);
    #endregion
    #region Doctor Consultation Job Order
    /// <summary>
    /// Gets the doctor consultation job order.
    /// </summary>
    public Task<QueryResponse<DoctorConsultationJobOrderResponse>> GetDoctorConsultationJobOrder(GetDoctorConsultationJobOrderRequest request);
    /// <summary>
    /// Gets the doctor consultation job order list.
    /// </summary>
    public Task<QueryResponse<List<DoctorConsultationJobOrderResponse>>> GetDoctorConsultationJobOrderList(GetDoctorConsultationJobOrderListRequest request);
    /// <summary>
    /// Creates a new doctor consultation job order.
    /// </summary>
    public Task<CmdResponse<CreateDoctorConsultationJobOrderRequest>> CreateDoctorConsultationJobOrder(CreateDoctorConsultationJobOrderRequest request);
    /// <summary>
    /// Updates the doctor consultation job order.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorConsultationJobOrderRequest>> UpdateDoctorConsultationJobOrder(UpdateDoctorConsultationJobOrderRequest request);
    /// <summary>
    /// Deletes the doctor consultation job order.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorConsultationJobOrderRequest>> DeleteDoctorConsultationJobOrder(DeleteDoctorConsultationJobOrderRequest request);
    #endregion
    #region Doctor Entity
    /// <summary>
    /// Gets the doctor entity.
    /// </summary>
    public Task<QueryResponse<DoctorEntityResponse>> GetDoctorEntity(GetDoctorEntityRequest request);
    /// <summary>
    /// Gets the doctor entity list.
    /// </summary>
    public Task<QueryResponse<List<DoctorEntityResponse>>> GetDoctorEntityList(GetDoctorEntityListRequest request);
    /// <summary>
    /// Creates a new doctor entity.
    /// </summary>
    public Task<CmdResponse<CreateDoctorEntityRequest>> CreateDoctorEntity(CreateDoctorEntityRequest request);
    /// <summary>
    /// Updates the doctor entity.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorEntityRequest>> UpdateDoctorEntity(UpdateDoctorEntityRequest request);
    /// <summary>
    /// Deletes the doctor entity.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorEntityRequest>> DeleteDoctorEntity(DeleteDoctorEntityRequest request);
    #endregion
    #region Doctor Entity Group
    /// <summary>
    /// Gets the doctor entity group.
    /// </summary>
    public Task<QueryResponse<DoctorEntityGroupResponse>> GetDoctorEntityGroup(GetDoctorEntityGroupRequest request);
    /// <summary>
    /// Gets the doctor entity group list.
    /// </summary>
    public Task<QueryResponse<List<DoctorEntityGroupResponse>>> GetDoctorEntityGroupList(GetDoctorEntityGroupListRequest request);
    /// <summary>
    /// Creates a new doctor entity group.
    /// </summary>
    public Task<CmdResponse<CreateDoctorEntityGroupRequest>> CreateDoctorEntityGroup(CreateDoctorEntityGroupRequest request);
    /// <summary>
    /// Updates the doctor entity group.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorEntityGroupRequest>> UpdateDoctorEntityGroup(UpdateDoctorEntityGroupRequest request);
    /// <summary>
    /// Deletes the doctor entity group.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorEntityGroupRequest>> DeleteDoctorEntityGroup(DeleteDoctorEntityGroupRequest request);
    #endregion
    #region Doctor Tag
    /// <summary>
    /// Gets the doctor tag.
    /// </summary>
    public Task<QueryResponse<DoctorTagResponse>> GetDoctorTag(GetDoctorTagRequest request);
    /// <summary>
    /// Gets the doctor tag list.
    /// </summary>
    public Task<QueryResponse<List<DoctorTagResponse>>> GetDoctorTagList(GetDoctorTagListRequest request);
    /// <summary>
    /// Creates a new doctor tag.
    /// </summary>
    public Task<CmdResponse<CreateDoctorTagRequest>> CreateDoctorTag(CreateDoctorTagRequest request);
    /// <summary>
    /// Updates the doctor tag.
    /// </summary>
    public Task<CmdResponse<UpdateDoctorTagRequest>> UpdateDoctorTag(UpdateDoctorTagRequest request);
    /// <summary>
    /// Deletes the doctor tag.
    /// </summary>
    public Task<CmdResponse<DeleteDoctorTagRequest>> DeleteDoctorTag(DeleteDoctorTagRequest request);
    #endregion
    // No Supported Consultation in Database
    #region Supported Consultation
    public Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request);
    public Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request);
    #endregion
    #endregion

    #region Patient Portal
    #region Patient
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
    #region Patient Consultation
    /// <summary>
    /// Gets the patient consultation.
    /// </summary>
    public Task<QueryResponse<PatientConsultationResponse>> GetPatientConsultation(GetPatientConsultationRequest request);
    /// <summary>
    /// Gets the patient consultation list.
    /// </summary>
    public Task<QueryResponse<List<PatientConsultationResponse>>> GetPatientConsultationList(GetPatientConsultationListRequest request);
    /// <summary>
    ///  Creates a new patient consultation.
    /// </summary>
    public Task<CmdResponse<CreatePatientConsultationRequest>> CreatePatientConsultation(CreatePatientConsultationRequest request);
    /// <summary>
    ///  Updates the patient consultation.
    /// </summary>
    public Task<CmdResponse<UpdatePatientConsultationRequest>> UpdatePatientConsultation(UpdatePatientConsultationRequest request);
    /// <summary>
    /// Deletes the patient consultation.
    /// </summary>
    public Task<CmdResponse<DeletePatientConsultationRequest>> DeletePatientConsultation(DeletePatientConsultationRequest request);
    #endregion
    #endregion
    
    #region Pharmacy Portal
    #region Pharmacy
    /// <summary>
    ///  Gets medicine auto complete list.
    /// </summary>
    public Task<QueryResponse<List<string>>> GetMedicineAutoComplete(GetMedicineAutoCompleteRequest request);
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
    #region Pharmacy Job Order
    /// <summary>
    ///  Gets the Pharmacy job order.
    /// </summary>
    public Task<QueryResponse<PharmacyJobOrderResponse>> GetPharmacyJobOrder(GetPharmacyJobOrderRequest request);
    /// <summary>
    ///  Gets the Pharmacy job order list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyJobOrderResponse>>> GetPharmacyJobOrderList(GetPharmacyJobOrderListRequest request);
    /// <summary>
    ///  Creates a new Pharmacy job order in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyJobOrderRequest>> CreatePharmacyJobOrder(CreatePharmacyJobOrderRequest request);
    /// <summary>
    /// Updates the Pharmacy job order.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyJobOrderRequest>> UpdatePharmacyJobOrder(UpdatePharmacyJobOrderRequest request);
    /// <summary>
    /// Deletes the Pharmacy job order.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyJobOrderRequest>> DeletePharmacyJobOrder(DeletePharmacyJobOrderRequest request);
    #endregion
    #region Pharmacy Entity
    /// <summary>
    /// Gets the Pharmacy entity.
    /// </summary>
    public Task<QueryResponse<PharmacyEntityResponse>> GetPharmacyEntity(GetPharmacyEntityRequest request);
    /// <summary>
    /// Gets the Pharmacy entity list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyEntityResponse>>> GetPharmacyEntityList(GetPharmacyEntityListRequest request);
    /// <summary>
    /// Creates a new Pharmacy entity in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyEntityRequest>> CreatePharmacyEntity(CreatePharmacyEntityRequest request);
    /// <summary>
    /// Updates the Pharmacy entity.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyEntityRequest>> UpdatePharmacyEntity(UpdatePharmacyEntityRequest request);
    /// <summary>
    /// Deletes the Pharmacy entity.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyEntityRequest>> DeletePharmacyEntity(DeletePharmacyEntityRequest request);
    

    #endregion
    #region Pharmacy Job Order Consultation Job Order
    /// <summary>
    /// Gets the Pharmacy job order consultation job order.
    /// </summary>
    public Task<QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>> GetPharmacyJobOrderConsultationJobOrder(GetPharmacyJobOrderConsultationJobOrderRequest request);
    /// <summary>
    ///  Gets the Pharmacy job order consultation job order list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>> GetPharmacyJobOrderConsultationJobOrderList(GetPharmacyJobOrderConsultationJobOrderListRequest request);
    /// <summary>
    /// Creates a new Pharmacy job order consultation job order in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyJobOrderConsultationJobOrderRequest>> CreatePharmacyJobOrderConsultationJobOrder(CreatePharmacyJobOrderConsultationJobOrderRequest request);
    /// <summary>
    /// Updates the Pharmacy job order consultation job order.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderRequest>> UpdatePharmacyJobOrderConsultationJobOrder(UpdatePharmacyJobOrderConsultationJobOrderRequest request);
    /// <summary>
    /// Deletes the Pharmacy job order consultation job order.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyJobOrderConsultationJobOrderRequest>> DeletePharmacyJobOrderConsultationJobOrder(DeletePharmacyJobOrderConsultationJobOrderRequest request);
    #endregion
    #region Pharmacy Job Order Medicine
    /// <summary>
    /// Gets the Pharmacy job order medicine.
    /// </summary>
    public Task<QueryResponse<PharmacyJobOrderMedicineResponse>> GetPharmacyJobOrderMedicine(GetPharmacyJobOrderMedicineRequest request);
    /// <summary>
    ///  Gets the Pharmacy job order medicine list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyJobOrderMedicineResponse>>> GetPharmacyJobOrderMedicineList(GetPharmacyJobOrderMedicineListRequest request);
    /// <summary>
    ///  Creates a new Pharmacy job order medicine in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyJobOrderMedicineRequest>> CreatePharmacyJobOrderMedicine(CreatePharmacyJobOrderMedicineRequest request);
    /// <summary>
    /// Updates the Pharmacy job order medicine.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyJobOrderMedicineRequest>> UpdatePharmacyJobOrderMedicine(UpdatePharmacyJobOrderMedicineRequest request);
    /// <summary>
    /// Deletes the Pharmacy job order medicine.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyJobOrderMedicineRequest>> DeletePharmacyJobOrderMedicine(DeletePharmacyJobOrderMedicineRequest request);
    #endregion
    #region Pharmacy Service Entity Group
    /// <summary>
    /// Gets the Pharmacy service entity group.
    /// </summary>
    public Task<QueryResponse<PharmacyServiceEntityGroupResponse>> GetPharmacyServiceEntityGroup(GetPharmacyServiceEntityGroupRequest request);
    /// <summary>
    /// Gets the Pharmacy service entity group list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyServiceEntityGroupResponse>>> GetPharmacyServiceEntityGroupList(GetPharmacyServiceEntityGroupListRequest request);
    /// <summary>
    /// Creates a new Pharmacy service entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyServiceEntityGroupRequest>> CreatePharmacyServiceEntityGroup(CreatePharmacyServiceEntityGroupRequest request);
    /// <summary>
    /// Updates the Pharmacy service entity group.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyServiceEntityGroupRequest>> UpdatePharmacyServiceEntityGroup(UpdatePharmacyServiceEntityGroupRequest request);
    /// <summary>
    /// Deletes the Pharmacy service entity group.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyServiceEntityGroupRequest>> DeletePharmacyServiceEntityGroup(DeletePharmacyServiceEntityGroupRequest request);
    #endregion
    #region Pharmacy Service Entity
    /// <summary>
    /// Gets the Pharmacy service entity.
    /// </summary>
    public Task<QueryResponse<PharmacyServiceEntityResponse>> GetPharmacyServiceEntity(GetPharmacyServiceEntityRequest request);
    /// <summary>
    ///  Gets the Pharmacy service entity list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyServiceEntityResponse>>> GetPharmacyServiceEntityList(GetPharmacyServiceEntityListRequest request);
    /// <summary>
    /// Creates a new Pharmacy service entity in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyServiceEntityRequest>> CreatePharmacyServiceEntity(CreatePharmacyServiceEntityRequest request);
    /// <summary>
    /// Updates the Pharmacy service entity.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyServiceEntityRequest>> UpdatePharmacyServiceEntity(UpdatePharmacyServiceEntityRequest request);
    /// <summary>
    /// Deletes the Pharmacy service entity.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyServiceEntityRequest>> DeletePharmacyServiceEntity(DeletePharmacyServiceEntityRequest request);
    #endregion
    #region Pharmacy Service
    /// <summary>
    /// Gets the Pharmacy service.
    /// </summary>
    public Task<QueryResponse<PharmacyServiceResponse>> GetPharmacyService(GetPharmacyServiceRequest request);
    /// <summary>
    /// Gets the Pharmacy service list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyServiceResponse>>> GetPharmacyServiceList(GetPharmacyServiceListRequest request);
    /// <summary>
    /// Creates a new Pharmacy service in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyServiceRequest>> CreatePharmacyService(CreatePharmacyServiceRequest request);
    /// <summary>
    /// Updates the Pharmacy service.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyServiceRequest>> UpdatePharmacyService(UpdatePharmacyServiceRequest request);
    /// <summary>
    /// Deletes the Pharmacy service.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyServiceRequest>> DeletePharmacyService(DeletePharmacyServiceRequest request);
    #endregion
    #region Pharmacy Service Tag
    /// <summary>
    /// Gets the Pharmacy service tag.
    /// </summary>
    public Task<QueryResponse<PharmacyServiceTagResponse>> GetPharmacyServiceTag(GetPharmacyServiceTagRequest request);
    /// <summary>
    ///  Gets the Pharmacy service tag list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyServiceTagResponse>>> GetPharmacyServiceTagList(GetPharmacyServiceTagListRequest request);
    /// <summary>
    /// Creates a new Pharmacy service tag in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyServiceTagRequest>> CreatePharmacyServiceTag(CreatePharmacyServiceTagRequest request);
    /// <summary>
    /// Updates the Pharmacy service tag.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyServiceTagRequest>> UpdatePharmacyServiceTag(UpdatePharmacyServiceTagRequest request);
    /// <summary>
    /// Deletes the Pharmacy service tag.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyServiceTagRequest>> DeletePharmacyServiceTag(DeletePharmacyServiceTagRequest request);
    #endregion
    #region Pharmacy Stock
    /// <summary>
    /// Gets the Pharmacy stock.
    /// </summary>
    public Task<QueryResponse<PharmacyStockResponse>> GetPharmacyStock(GetPharmacyStockRequest request);
    /// <summary>
    /// Gets the Pharmacy stock list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyStockResponse>>> GetPharmacyStockList(GetPharmacyStockListRequest request);
    /// <summary>
    /// Creates a new Pharmacy stock in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyStockRequest>> CreatePharmacyStock(CreatePharmacyStockRequest request);
    /// <summary>
    /// Updates the Pharmacy stock.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyStockRequest>> UpdatePharmacyStock(UpdatePharmacyStockRequest request);
    /// <summary>
    /// Deletes the Pharmacy stock.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyStockRequest>> DeletePharmacyStock(DeletePharmacyStockRequest request);
    #endregion
    #region Pharmacy Tag
    /// <summary>
    /// Gets the Pharmacy tag.
    /// </summary>
    public Task<QueryResponse<PharmacyTagResponse>> GetPharmacyTag(GetPharmacyTagRequest request);
    /// <summary>
    /// Gets the Pharmacy tag list.
    /// </summary>
    public Task<QueryResponse<List<PharmacyTagResponse>>> GetPharmacyTagList(GetPharmacyTagListRequest request);
    /// <summary>
    /// Creates a new Pharmacy tag in the system.
    /// </summary>
    public Task<CmdResponse<CreatePharmacyTagRequest>> CreatePharmacyTag(CreatePharmacyTagRequest request);
    /// <summary>
    /// Updates the Pharmacy tag.
    /// </summary>
    public Task<CmdResponse<UpdatePharmacyTagRequest>> UpdatePharmacyTag(UpdatePharmacyTagRequest request);
    /// <summary>
    /// Deletes the Pharmacy tag.
    /// </summary>
    public Task<CmdResponse<DeletePharmacyTagRequest>> DeletePharmacyTag(DeletePharmacyTagRequest request);
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
    #region Laboratory Job Order Result
    /// <summary>
    /// Gets the laboratory job order result profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryJobOrderResultResponse>> GetLaboratoryJobOrderResult(GetLaboratoryJobOrderResultRequest request);
    /// <summary>
    /// Get all laboratory job order results in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryJobOrderResultResponse>>> GetLaboratoryJobOrderResultList(GetLaboratoryJobOrderResultListRequest request);
    /// <summary>
    /// Creates a new laboratory job order result in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryJobOrderResultRequest>> CreateLaboratoryJobOrderResult(CreateLaboratoryJobOrderResultRequest request);
    /// <summary>
    /// Updates the laboratory job order result profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryJobOrderResultRequest>> UpdateLaboratoryJobOrderResult(UpdateLaboratoryJobOrderResultRequest request);
    /// <summary>
    /// Deletes the laboratory job order result from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryJobOrderResultRequest>> DeleteLaboratoryJobOrderResult(DeleteLaboratoryJobOrderResultRequest request);
    #endregion
    #region Laboratory Job Order Result File
    /// <summary>
    ///  Gets the laboratory service entity group profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryJobOrderResultFileResponse>> GetLaboratoryJobOrderResultFile(GetLaboratoryJobOrderResultFileRequest request);
    /// <summary>
    ///  Get all laboratory service entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryJobOrderResultFileResponse>>> GetLaboratoryJobOrderResultFileList(GetLaboratoryJobOrderResultFileListRequest request);
    /// <summary>
    ///  Creates a new laboratory service entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryJobOrderResultFileRequest>> CreateLaboratoryJobOrderResultFile(CreateLaboratoryJobOrderResultFileRequest request);
    /// <summary>
    ///  Updates the laboratory service entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryJobOrderResultFileRequest>> UpdateLaboratoryJobOrderResultFile(UpdateLaboratoryJobOrderResultFileRequest request);
    /// <summary>
    ///  Deletes the laboratory service entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryJobOrderResultFileRequest>> DeleteLaboratoryJobOrderResultFile(DeleteLaboratoryJobOrderResultFileRequest request);
    #endregion
    #region Laboratory Job Order Detail
    /// <summary>
    /// Gets the laboratory job order detail profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryJobOrderDetailResponse>> GetLaboratoryJobOrderDetail(GetLaboratoryJobOrderDetailRequest request);
    /// <summary>
    /// Get all laboratory job order details in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryJobOrderDetailResponse>>> GetLaboratoryJobOrderDetailList(GetLaboratoryJobOrderDetailListRequest request);
    /// <summary>
    /// Creates a new laboratory job order detail in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryJobOrderDetailRequest>> CreateLaboratoryJobOrderDetail(CreateLaboratoryJobOrderDetailRequest request);
    /// <summary>
    /// Updates the laboratory job order detail profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryJobOrderDetailRequest>> UpdateLaboratoryJobOrderDetail(UpdateLaboratoryJobOrderDetailRequest request);
    /// <summary>
    /// Deletes the laboratory job order detail from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryJobOrderDetailRequest>> DeleteLaboratoryJobOrderDetail(DeleteLaboratoryJobOrderDetailRequest request);
    #endregion
    #region Laboratory Location Tag
    /// <summary>
    /// Gets the laboratory location tag profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryLocationTagResponse>> GetLaboratoryLocationTag(GetLaboratoryLocationTagRequest request);
    /// <summary>
    /// Get all laboratory location tags in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryLocationTagResponse>>> GetLaboratoryLocationTagList(GetLaboratoryLocationTagListRequest request);
    /// <summary>
    /// Creates a new laboratory location tag in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryLocationTagRequest>> CreateLaboratoryLocationTag(CreateLaboratoryLocationTagRequest request);
    /// <summary>
    /// Updates the laboratory location tag profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryLocationTagRequest>> UpdateLaboratoryLocationTag(UpdateLaboratoryLocationTagRequest request);
    /// <summary>
    /// Deletes the laboratory location tag from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryLocationTagRequest>> DeleteLaboratoryLocationTag(DeleteLaboratoryLocationTagRequest request);
    #endregion
    #region Laboratory Service Tag
    /// <summary>
    /// Gets the laboratory service tag profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryServiceTagResponse>> GetLaboratoryServiceTag(GetLaboratoryServiceTagRequest request);
    /// <summary>
    /// Get all laboratory service tags in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryServiceTagResponse>>> GetLaboratoryServiceTagList(GetLaboratoryServiceTagListRequest request);
    /// <summary>
    /// Creates a new laboratory service tag in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryServiceTagRequest>> CreateLaboratoryServiceTag(CreateLaboratoryServiceTagRequest request);
    /// <summary>
    /// Updates the laboratory service tag profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryServiceTagRequest>> UpdateLaboratoryServiceTag(UpdateLaboratoryServiceTagRequest request);
    /// <summary>
    /// Deletes the laboratory service tag from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryServiceTagRequest>> DeleteLaboratoryServiceTag(DeleteLaboratoryServiceTagRequest request);
    

    #endregion
    #region Laboratory Tag
    /// <summary>
    /// Gets the laboratory tag profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryTagResponse>> GetLaboratoryTag(GetLaboratoryTagRequest request);
    /// <summary>
    /// Get all laboratory tags in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryTagResponse>>> GetLaboratoryTagList(GetLaboratoryTagListRequest request);
    /// <summary>
    /// Creates a new laboratory tag in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryTagRequest>> CreateLaboratoryTag(CreateLaboratoryTagRequest request);
    /// <summary>
    /// Updates the laboratory tag profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryTagRequest>> UpdateLaboratoryTag(UpdateLaboratoryTagRequest request);
    /// <summary>
    /// Deletes the laboratory tag from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryTagRequest>> DeleteLaboratoryTag(DeleteLaboratoryTagRequest request);
    #endregion
    #region Laboratory Entity
    /// <summary>
    /// Gets the laboratory entity profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryEntityResponse>> GetLaboratoryEntity(GetLaboratoryEntityRequest request);
    /// <summary>
    /// Get all laboratory entities in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryEntityResponse>>> GetLaboratoryEntityList(GetLaboratoryEntityListRequest request);
    /// <summary>
    /// Creates a new laboratory entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryEntityRequest>> CreateLaboratoryEntity(CreateLaboratoryEntityRequest request);
    /// <summary>
    /// Updates the laboratory entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryEntityRequest>> UpdateLaboratoryEntity(UpdateLaboratoryEntityRequest request);
    /// <summary>
    /// Deletes the laboratory entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryEntityRequest>> DeleteLaboratoryEntity(DeleteLaboratoryEntityRequest request);
    #endregion
    #region Laboratory Entity Group
    /// <summary>
    /// Gets the laboratory entity group profile.
    /// </summary>
    public Task<QueryResponse<LaboratoryEntityGroupResponse>> GetLaboratoryEntityGroup(GetLaboratoryEntityGroupRequest request);
    /// <summary>
    /// Get all laboratory entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<LaboratoryEntityGroupResponse>>> GetLaboratoryEntityGroupList(GetLaboratoryEntityGroupListRequest request);
    /// <summary>
    /// Creates a new laboratory entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateLaboratoryEntityGroupRequest>> CreateLaboratoryEntityGroup(CreateLaboratoryEntityGroupRequest request);
    /// <summary>
    /// Updates the laboratory entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateLaboratoryEntityGroupRequest>> UpdateLaboratoryEntityGroup(UpdateLaboratoryEntityGroupRequest request);
    /// <summary>
    /// Deletes the laboratory entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteLaboratoryEntityGroupRequest>> DeleteLaboratoryEntityGroup(DeleteLaboratoryEntityGroupRequest request);
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
    public Task<CmdResponse<DeleteConsultationPaymentRequest>> DeleteConsultationPayment(DeleteConsultationPaymentRequest request);

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
    #region Consultation Entity Group
    /// <summary>
    /// Gets the consultation entity group profile.
    /// </summary>
    public Task<QueryResponse<ConsultationEntityGroupResponse>> GetConsultationEntityGroup(GetConsultationEntityGroupRequest request);
    /// <summary>
    /// Get all consultation entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationEntityGroupResponse>>> GetConsultationEntityGroupList(GetConsultationEntityGroupListRequest request);
    /// <summary>
    /// Creates a new consultation entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationEntityGroupRequest>> CreateConsultationEntityGroup(CreateConsultationEntityGroupRequest request);
    /// <summary>
    ///  Updates the consultation entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationEntityGroupRequest>> UpdateConsultationEntityGroup(UpdateConsultationEntityGroupRequest request);
    /// <summary>
    /// Deletes the consultation entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationEntityGroupRequest>> DeleteConsultationEntityGroup(DeleteConsultationEntityGroupRequest request);
    #endregion
    #region Consultation Job Order Laboratory
    /// <summary>
    /// Gets the consultation job order laboratory profile.
    /// </summary>
    public Task<QueryResponse<ConsultationJobOrderLaboratoryResponse>> GetConsultationJobOrderLaboratory(GetConsultationJobOrderLaboratoryRequest request);
    /// <summary>
    /// Get all consultation job order laboratories in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationJobOrderLaboratoryResponse>>> GetConsultationJobOrderLaboratoryList(GetConsultationJobOrderLaboratoryListRequest request);
    /// <summary>
    /// Creates a new consultation job order laboratory in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationJobOrderLaboratoryRequest>> CreateConsultationJobOrderLaboratory(CreateConsultationJobOrderLaboratoryRequest request);
    /// <summary>
    /// Updates the consultation job order laboratory profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationJobOrderLaboratoryRequest>> UpdateConsultationJobOrderLaboratory(UpdateConsultationJobOrderLaboratoryRequest request);
    /// <summary>
    /// Deletes the consultation job order laboratory from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationJobOrderLaboratoryRequest>> DeleteConsultationJobOrderLaboratory(DeleteConsultationJobOrderLaboratoryRequest request);
    #endregion
    #region Consultation Job Order
    /// <summary>
    /// Gets the consultation job order profile.
    /// </summary>
    public Task<QueryResponse<ConsultationJobOrderResponse>> GetConsultationJobOrder(GetConsultationJobOrderRequest request);
    /// <summary>
    /// Get all consultation job orders in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationJobOrderResponse>>> GetConsultationJobOrderList(GetConsultationJobOrderListRequest request);
    /// <summary>
    /// Creates a new consultation job order in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationJobOrderRequest>> CreateConsultationJobOrder(CreateConsultationJobOrderRequest request);
    /// <summary>
    /// Updates the consultation job order profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationJobOrderRequest>> UpdateConsultationJobOrder(UpdateConsultationJobOrderRequest request);
    /// <summary>
    /// Deletes the consultation job order from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationJobOrderRequest>> DeleteConsultationJobOrder(DeleteConsultationJobOrderRequest request);
    #endregion
    #region Consultation Job Order Medicine
    /// <summary>
    /// Gets the consultation job order medicine profile.
    /// </summary>
    public Task<QueryResponse<ConsultationJobOrderMedicineResponse>> GetConsultationJobOrderMedicine(GetConsultationJobOrderMedicineRequest request);
    /// <summary>
    /// Get all consultation job order medicines in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationJobOrderMedicineResponse>>> GetConsultationJobOrderMedicineList(GetConsultationJobOrderMedicineListRequest request);
    /// <summary>
    /// Creates a new consultation job order medicine in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationJobOrderMedicineRequest>> CreateConsultationJobOrderMedicine(CreateConsultationJobOrderMedicineRequest request);
    /// <summary>
    /// Updates the consultation job order medicine profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationJobOrderMedicineRequest>> UpdateConsultationJobOrderMedicine(UpdateConsultationJobOrderMedicineRequest request);
    /// <summary>
    /// Deletes the consultation job order medicine from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationJobOrderMedicineRequest>> DeleteConsultationJobOrderMedicine(DeleteConsultationJobOrderMedicineRequest request);
    #endregion
    #region Consultation Tag
    /// <summary>
    /// Gets the consultation tag profile.
    /// </summary>
    public Task<QueryResponse<ConsultationTagResponse>> GetConsultationTag(GetConsultationTagRequest request);
    /// <summary>
    /// Get all consultation tags in the system.
    /// </summary>
    public Task<QueryResponse<List<ConsultationTagResponse>>> GetConsultationTagList(GetConsultationTagListRequest request);
    /// <summary>
    /// Creates a new consultation tag in the system.
    /// </summary>
    public Task<CmdResponse<CreateConsultationTagRequest>> CreateConsultationTag(CreateConsultationTagRequest request);
    /// <summary>
    ///  Updates the consultation tag profile.
    /// </summary>
    public Task<CmdResponse<UpdateConsultationTagRequest>> UpdateConsultationTag(UpdateConsultationTagRequest request);
    /// <summary>
    /// Deletes the consultation tag from the system.
    /// </summary>
    public Task<CmdResponse<DeleteConsultationTagRequest>> DeleteConsultationTag(DeleteConsultationTagRequest request);
    #endregion
    #endregion
    
    #region Administrator Portal
    
    #region Member
    public Task<QueryResponse<List<CredentialResponse>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request);

    #endregion

    #endregion
    
    #region Ailment Portal
    #region Ailment
    /// <summary>
    /// Gets the ailment profile.
    /// </summary>
    public Task<QueryResponse<AilmentResponse>> GetAilment(GetAilmentRequest request);
    /// <summary>
    /// Gets all ailments in the system.
    /// </summary>
    public Task<QueryResponse<List<AilmentResponse>>> GetAilmentList(GetAilmentListRequest request);
    /// <summary>
    /// Creates a new ailment in the system.
    /// </summary>
    public Task<CmdResponse<CreateAilmentRequest>> CreateAilment(CreateAilmentRequest request);
    /// <summary>
    /// Updates the ailment profile.
    /// </summary>
    public Task<CmdResponse<UpdateAilmentRequest>> UpdateAilment(UpdateAilmentRequest request);
    /// <summary>
    /// Deletes the ailment from the system.
    /// </summary>
    public Task<CmdResponse<DeleteAilmentRequest>> DeleteAilment(DeleteAilmentRequest request);
    #endregion
    #region Ailment Entity
    /// <summary>
    /// Gets the ailment entity profile.
    /// </summary>
    public Task<QueryResponse<AilmentEntityResponse>> GetAilmentEntity(GetAilmentEntityRequest request);
    /// <summary>
    /// Gets all ailment entities in the system.
    /// </summary>
    public Task<QueryResponse<List<AilmentEntityResponse>>> GetAilmentEntityList(GetAilmentEntityListRequest request);
    /// <summary>
    /// Creates a new ailment entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateAilmentEntityRequest>> CreateAilmentEntity(CreateAilmentEntityRequest request);
    /// <summary>
    ///  Updates the ailment entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateAilmentEntityRequest>> UpdateAilmentEntity(UpdateAilmentEntityRequest request);
    /// <summary>
    /// Deletes the ailment entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteAilmentEntityRequest>> DeleteAilmentEntity(DeleteAilmentEntityRequest request);
    #endregion
    #region Ailment Entity Group
    /// <summary>
    ///  Gets the ailment entity group profile.
    /// </summary>
    public Task<QueryResponse<AilmentEntityGroupResponse>> GetAilmentEntityGroup(GetAilmentEntityGroupRequest request);
    /// <summary>
    /// Get all ailment entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<AilmentEntityGroupResponse>>> GetAilmentEntityGroupList(GetAilmentEntityGroupListRequest request);
    /// <summary>
    /// Creates a new ailment entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateAilmentEntityGroupRequest>> CreateAilmentEntityGroup(CreateAilmentEntityGroupRequest request);
    /// <summary>
    /// Updates the ailment entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateAilmentEntityGroupRequest>> UpdateAilmentEntityGroup(UpdateAilmentEntityGroupRequest request);
    /// <summary>
    /// Deletes the ailment entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteAilmentEntityGroupRequest>> DeleteAilmentEntityGroup(DeleteAilmentEntityGroupRequest request);
    #endregion
    #region Ailment Tag
    /// <summary>
    /// Gets the ailment tag profile.
    /// </summary>
    public Task<QueryResponse<AilmentTagResponse>> GetAilmentTag(GetAilmentTagRequest request);
    /// <summary>
    ///  Gets all ailment tags in the system.
    /// </summary>
    public Task<QueryResponse<List<AilmentTagResponse>>> GetAilmentTagList(GetAilmentTagListRequest request);
    /// <summary>
    /// Creates a new ailment tag in the system.
    /// </summary>
    public Task<CmdResponse<CreateAilmentTagRequest>> CreateAilmentTag(CreateAilmentTagRequest request);
    /// <summary>
    ///  Updates the ailment tag profile.
    /// </summary>
    public Task<CmdResponse<UpdateAilmentTagRequest>> UpdateAilmentTag(UpdateAilmentTagRequest request);
    /// <summary>
    /// Deletes the ailment tag from the system.
    /// </summary>
    public Task<CmdResponse<DeleteAilmentTagRequest>> DeleteAilmentTag(DeleteAilmentTagRequest request);
    

    #endregion
    #endregion
    
    #region Hospital Portal

    

    #endregion
    
    #region Meta Data Portal
    #region Meta Data Entity Group
    /// <summary>
    /// Gets the meta data entity group profile.
    /// </summary>
    public Task<QueryResponse<MetaDataEntityGroupResponse>> GetMetaDataEntityGroup(GetMetaDataEntityGroupRequest request);
    /// <summary>
    /// Gets all meta data entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<MetaDataEntityGroupResponse>>> GetMetaDataEntityGroupList(GetMetaDataEntityGroupListRequest request);
    /// <summary>
    /// Creates a new meta data entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateMetaDataEntityGroupRequest>> CreateMetaDataEntityGroup(CreateMetaDataEntityGroupRequest request);
    /// <summary>
    ///  Updates the meta data entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateMetaDataEntityGroupRequest>> UpdateMetaDataEntityGroup(UpdateMetaDataEntityGroupRequest request);
    /// <summary>
    /// Deletes the meta data entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMetaDataEntityGroupRequest>> DeleteMetaDataEntityGroup(DeleteMetaDataEntityGroupRequest request);
    #endregion
    #region Meta Data Entity
    /// <summary>
    ///  Gets the meta data entity profile.
    /// </summary>
    public Task<QueryResponse<MetaDataEntityResponse>> GetMetaDataEntity(GetMetaDataEntityRequest request);
    /// <summary>
    ///  Get all meta data entities in the system.
    /// </summary>
    public Task<QueryResponse<List<MetaDataEntityResponse>>> GetMetaDataEntityList(GetMetaDataEntityListRequest request);
    /// <summary>
    ///  Creates a new meta data entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateMetaDataEntityRequest>> CreateMetaDataEntity(CreateMetaDataEntityRequest request);
    /// <summary>
    ///  Updates the meta data entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateMetaDataEntityRequest>> UpdateMetaDataEntity(UpdateMetaDataEntityRequest request);
    /// <summary>
    ///  Deletes the meta data entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMetaDataEntityRequest>> DeleteMetaDataEntity(DeleteMetaDataEntityRequest request);

    #endregion
    #region MetaDatum
    /// <summary>
    /// Gets the meta data profile.
    /// </summary>
    public Task<QueryResponse<MetaDatumResponse>> GetMetaDatum(GetMetaDatumRequest request);
    /// <summary>
    /// Gets all meta data in the system.
    /// </summary>
    public Task<QueryResponse<List<MetaDatumResponse>>> GetMetaDatumList(GetMetaDatumListRequest request);
    /// <summary>
    /// Creates a new meta data in the system.
    /// </summary>
    public Task<CmdResponse<CreateMetaDatumRequest>> CreateMetaDatum(CreateMetaDatumRequest request);
    /// <summary>
    ///  Updates the meta data profile.
    /// </summary>
    public Task<CmdResponse<UpdateMetaDatumRequest>> UpdateMetaDatum(UpdateMetaDatumRequest request);
    /// <summary>
    /// Deletes the meta data from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMetaDatumRequest>> DeleteMetaDatum(DeleteMetaDatumRequest request);
    #endregion
    #endregion
    
    #region Vendor Portal

    

    #endregion

    #region Schedule Portal
    #region Schedule
    /// <summary>
    /// Gets the schedule profile.
    /// </summary>
    public Task<QueryResponse<ScheduleResponse>> GetSchedule(GetScheduleRequest request);
    /// <summary>
    /// Gets all schedules in the system.
    /// </summary>
    public Task<QueryResponse<List<ScheduleResponse>>> GetScheduleList(GetScheduleListRequest request);
    /// <summary>
    /// Creates a new schedule in the system.
    /// </summary>
    public Task<CmdResponse<CreateScheduleRequest>> CreateSchedule(CreateScheduleRequest request);
    /// <summary>
    /// Updates the schedule profile.
    /// </summary>
    public Task<CmdResponse<UpdateScheduleRequest>> UpdateSchedule(UpdateScheduleRequest request);
    /// <summary>
    /// Deletes the schedule from the system.
    /// </summary>
    public Task<CmdResponse<DeleteScheduleRequest>> DeleteSchedule(DeleteScheduleRequest request);
    #endregion
    #endregion

    #region Unit Portal
    #region Unit
    /// <summary>
    /// Gets the unit profile.
    /// </summary>
    public Task<QueryResponse<UnitResponse>> GetUnit(GetUnitRequest request);
    /// <summary>
    /// Gets all units in the system.
    /// </summary>
    public Task<QueryResponse<List<UnitResponse>>> GetUnitList(GetUnitListRequest request);
    /// <summary>
    /// Creates a new unit in the system.
    /// </summary>
    public Task<CmdResponse<CreateUnitRequest>> CreateUnit(CreateUnitRequest request);
    /// <summary>
    /// Updates the unit profile.
    /// </summary>
    public Task<CmdResponse<UpdateUnitRequest>> UpdateUnit(UpdateUnitRequest request);
    /// <summary>
    /// Deletes the unit from the system.
    /// </summary>
    public Task<CmdResponse<DeleteUnitRequest>> DeleteUnit(DeleteUnitRequest request);
    #endregion
    #region Unit Entity
    /// <summary>
    /// Gets the unit entity profile.
    /// </summary>
    public Task<QueryResponse<UnitEntityResponse>> GetUnitEntity(GetUnitEntityRequest request);
    /// <summary>
    /// Gets all unit entities in the system.
    /// </summary>
    public Task<QueryResponse<List<UnitEntityResponse>>> GetUnitEntityList(GetUnitEntityListRequest request);
    /// <summary>
    /// Creates a new unit entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateUnitEntityRequest>> CreateUnitEntity(CreateUnitEntityRequest request);
    /// <summary>
    /// Updates the unit entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateUnitEntityRequest>> UpdateUnitEntity(UpdateUnitEntityRequest request);
    /// <summary>
    /// Deletes the unit entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteUnitEntityRequest>> DeleteUnitEntity(DeleteUnitEntityRequest request);
    #endregion
    #region Unit Entity Group
    /// <summary>
    ///  Gets the unit entity group profile.
    /// </summary>
    public Task<QueryResponse<UnitEntityGroupResponse>> GetUnitEntityGroup(GetUnitEntityGroupRequest request);
    /// <summary>
    /// Gets all unit entity groups in the system.
    /// </summary>
    public Task<QueryResponse<List<UnitEntityGroupResponse>>> GetUnitEntityGroupList(GetUnitEntityGroupListRequest request);
    /// <summary>
    /// Creates a new unit entity group in the system.
    /// </summary>
    public Task<CmdResponse<CreateUnitEntityGroupRequest>> CreateUnitEntityGroup(CreateUnitEntityGroupRequest request);
    /// <summary>
    /// Updates the unit entity group profile.
    /// </summary>
    public Task<CmdResponse<UpdateUnitEntityGroupRequest>> UpdateUnitEntityGroup(UpdateUnitEntityGroupRequest request);
    /// <summary>
    /// Deletes the unit entity group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteUnitEntityGroupRequest>> DeleteUnitEntityGroup(DeleteUnitEntityGroupRequest request);
    #endregion
    #endregion
}