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

namespace HealthEssentials.Integration.Drivers;

public class HealthEssentialsServiceDriver : DriverBase, IHealthEssentialsServiceWrapper
{
    public HealthEssentialsServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:HealthEssentialsService"));
    }


    public async Task<CmdResponse<UpdateDoctorRequest>> UpdateDoctor(UpdateDoctorRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyDoctor(VerifyDoctorRequest request)
    {
        return await SendAsync<VerifyDoctorRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyMemberResponse>>> GetPharmacyMemberList(GetPharmacyMemberListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyMember(VerifyPharmacyMemberRequest request)
    {
        return await SendAsync<VerifyPharmacyMemberRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<UpdatePharmacyMemberRequest>> UpdatePharmacyMember(UpdatePharmacyMemberRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<DeletePharmacyMemberRequest>> DeletePharmacyMember(DeletePharmacyMemberRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPatient(VerifyPatientRequest request)
    {
        return await SendAsync<VerifyPatientRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<DeleteLogisticRequest>> DeleteLogistic(DeleteLogisticRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticIdentity(VerifyLogisticIdentityRequest request)
    {
        return await SendAsync<VerifyLogisticIdentityRequest, IdentityValidationResponse>(request);
    }

    public async Task<QueryResponse<LogisticRiderHandleResponse>> GetLogisticRiderHandle(GetLogisticRiderHandleRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LogisticRiderHandleResponse>>> GetLogisticRiderHandleList(GetLogisticRiderHandleListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryMember(VerifyLaboratoryMemberRequest request)
    {
        return await SendAsync<VerifyLaboratoryMemberRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<DeleteLogisticRiderRequest>> DeleteLogisticRider(DeleteLogisticRiderRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LaboratoryResponse>> GetLaboratory(GetLaboratoryRequest request)
    {
        return await SendAsync<GetLaboratoryRequest, LaboratoryResponse>(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryMemberRequest>> DeleteLaboratoryMember(DeleteLaboratoryMemberRequest request)
    {
        throw new NotImplementedException();
    }

    async Task<QueryResponse<List<LaboratoryServiceResponse>>> IHealthEssentialsServiceWrapper.GetLaboratoryServiceList(GetLaboratoryServiceListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LaboratoryServiceResponse>> GetLaboratoryService(GetLaboratoryServiceRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceEntityList(GetLaboratoryServiceListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceListRequest, List<LaboratoryServiceEntityResponse>>(request);
    }

    public async Task<QueryResponse<ConsultationResponse>> GetConsultation(GetConsultationRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateConsultationRequest>> CreateConsultation(CreateConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationRequest>> UpdateConsultation(UpdateConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse> DeleteConsultation(DeleteConsultationRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateConsultationTypeGroupRequest>> CreateConsultationTypeGroup(CreateConsultationTypeGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationTypeRequest>> CreateConsultationType(CreateConsultationTypeRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorRequest>> DeleteDoctor(DeleteDoctorRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<AddSupportedConsultationRequest>> AddSupportedConsultation(AddSupportedConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateDoctorRequest>> CreateDoctor(CreateDoctorRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryRequest>> CreateLaboratory(CreateLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryRequest>> UpdateLaboratory(UpdateLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryRequest>> DeleteLaboratory(DeleteLaboratoryRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LaboratoryMemberResponse>> GetLaboratoryMember(GetLaboratoryMemberRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LaboratoryMemberResponse>>> GetLaboratoryMemberList(GetLaboratoryMemberListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<UpdateLaboratoryMemberRequest>> UpdateLaboratoryMember(UpdateLaboratoryMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryMemberRequest>> CreateLaboratoryMember(CreateLaboratoryMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceRequest>> CreateLaboratoryService(CreateLaboratoryServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceRequest>> UpdateLaboratoryService(UpdateLaboratoryServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceRequest>> DeleteLaboratoryService(DeleteLaboratoryServiceRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LaboratoryServiceEntityResponse>> GetLaboratoryServiceEntity(GetLaboratoryServiceEntityRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceEntityList(GetLaboratoryServiceEntityListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceEntityRequest>> UpdateLaboratoryServiceEntity(UpdateLaboratoryServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceEntityRequest>> DeleteLaboratoryServiceEntity(DeleteLaboratoryServiceEntityRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LaboratoryServiceEntityGroupResponse>> GetLaboratoryServiceEntityGroup(GetLaboratoryServiceEntityGroupRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> GetLaboratoryServiceEntityGroupList(GetLaboratoryServiceEntityGroupListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceEntityGroupRequest>> UpdateLaboratoryServiceEntityGroup(UpdateLaboratoryServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceEntityGroupRequest>> DeleteLaboratoryServiceEntityGroup(DeleteLaboratoryServiceEntityGroupRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<ConsultationEntityResponse>>> GetConsultationEntityList(GetConsultationEntityListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<ConsultationEntityResponse>> GetConsultationEntity(GetConsultationEntityRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateConsultationEntityRequest>> CreateConsultationEntity(CreateConsultationEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationEntityRequest>> UpdateConsultationEntity(UpdateConsultationEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationEntityRequest>> DeleteConsultationEntity(DeleteConsultationEntityRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<ConsultationPaymentResponse>> GetConsultationPayment(GetConsultationPaymentRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<ConsultationPaymentResponse>>> GetConsultationPaymentList(GetConsultationPaymentListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateLaboratoryServiceEntityGroupRequest>> CreateLaboratoryServiceEntityGroup(CreateLaboratoryServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceEntityRequest>> CreateLaboratoryServiceEntity(CreateLaboratoryServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LogisticResponse>> GetLogistic(GetLogisticRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateLogisticRequest>> CreateLogistic(CreateLogisticRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLogisticRequest>> UpdateLogistic(UpdateLogisticRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateLogisticRiderHandleRequest>> CreateLogisticRiderHandle(CreateLogisticRiderHandleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLogisticRiderHandleRequest>> UpdateLogisticRiderHandle(UpdateLogisticRiderHandleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLogisticRiderHandleRequest>> DeleteLogisticRiderHandle(DeleteLogisticRiderHandleRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<LogisticRiderResponse>> GetLogisticRider(GetLogisticRiderRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<LogisticRiderResponse>>> GetLogisticRiderList(GetLogisticRiderListRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreateLogisticRiderRequest>> CreateLogisticRider(CreateLogisticRiderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLogisticRiderRequest>> UpdateLogisticRider(UpdateLogisticRiderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreatePatientRequest>> CreatePatient(CreatePatientRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyResponse>> GetPharmacy(GetPharmacyRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreatePharmacyRequest>> CreatePharmacy(CreatePharmacyRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyRequest>> UpdatePharmacy(UpdatePharmacyRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyRequest>> DeletePharmacy(DeletePharmacyRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<PharmacyMemberResponse>> GetPharmacyMember(GetPharmacyMemberRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<CreatePharmacyMemberRequest>> CreatePharmacyMember(CreatePharmacyMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationPaymentRequest>> CreateConsultationPayment(CreateConsultationPaymentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationPaymentRequest>> UpdateConsultationPayment(UpdateConsultationPaymentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse> DeleteConsultationPayment(DeleteConsultationPaymentRequest request)
    {
        throw new NotImplementedException();
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

    public async Task<CmdResponse<DeletePatientRequest>> DeletePatient(DeletePatientRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResponse<List<PharmacyResponse>>> GetPharmacyList(GetPharmacyListRequest request)
    {
        return await SendAsync<GetPharmacyListRequest, List<PharmacyResponse>>(request);
    }

    public async Task<QueryResponse<List<DoctorResponse>>> GetDoctorList(GetDoctorListRequest request)
    {
        return await SendAsync<GetDoctorListRequest, List<DoctorResponse>>(request);
    }


    public async Task<QueryResponse<PatientResponse>> GetPatient(GetPatientRequest request)
    {
        return await SendAsync<GetPatientRequest, PatientResponse>(request);
    }

    public async Task<QueryResponse<DoctorResponse>> GetDoctor(GetDoctorRequest request)
    {
        return await SendAsync<GetDoctorRequest, DoctorResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorConsultationResponse>>> GetSupportedConsultationList(GetSupportedConsultationListRequest request)
    {
        return await SendAsync<GetSupportedConsultationListRequest, List<DoctorConsultationResponse>>(request);
    }

    public async Task<QueryResponse<List<PatientResponse>>> GetPatientList(GetPatientListRequest request)
    {
        throw new NotImplementedException();
    }
 
    public async Task<CmdResponse<UpdatePatientRequest>> UpdatePatient(UpdatePatientRequest request)
    {
        throw new NotImplementedException();
    }
}   