using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Verify;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Verify;
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
using HealthEssentials.Integration.Interfaces;
using IdentityServer.Domain.Generic.Contracts.Responses;
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

    public async Task<CmdResponse<DeleteConsultationTagRequest>> DeleteConsultationTag(DeleteConsultationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<List<CredentialResponse>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request)
    {
        return await SendAsync<GetPendingRegistrationCompletionListRequest, List<CredentialResponse>>(request);
    }

    public async Task<QueryResponse<AilmentResponse>> GetAilment(GetAilmentRequest request)
    {
        return await SendAsync<GetAilmentRequest, AilmentResponse>(request);
    }

    public async Task<QueryResponse<List<AilmentResponse>>> GetAilmentList(GetAilmentListRequest request)
    {
        return await SendAsync<GetAilmentListRequest, List<AilmentResponse>>(request);
    }

    public async Task<CmdResponse<CreateAilmentRequest>> CreateAilment(CreateAilmentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateAilmentRequest>> UpdateAilment(UpdateAilmentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteAilmentRequest>> DeleteAilment(DeleteAilmentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<AilmentEntityResponse>> GetAilmentEntity(GetAilmentEntityRequest request)
    {
        return await SendAsync<GetAilmentEntityRequest, AilmentEntityResponse>(request);
    }

    public async Task<QueryResponse<List<AilmentEntityResponse>>> GetAilmentEntityList(GetAilmentEntityListRequest request)
    {
        return await SendAsync<GetAilmentEntityListRequest, List<AilmentEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateAilmentEntityRequest>> CreateAilmentEntity(CreateAilmentEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateAilmentEntityRequest>> UpdateAilmentEntity(UpdateAilmentEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteAilmentEntityRequest>> DeleteAilmentEntity(DeleteAilmentEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<AilmentEntityGroupResponse>> GetAilmentEntityGroup(GetAilmentEntityGroupRequest request)
    {
        return await SendAsync<GetAilmentEntityGroupRequest, AilmentEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<AilmentEntityGroupResponse>>> GetAilmentEntityGroupList(GetAilmentEntityGroupListRequest request)
    {
        return await SendAsync<GetAilmentEntityGroupListRequest, List<AilmentEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateAilmentEntityGroupRequest>> CreateAilmentEntityGroup(CreateAilmentEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateAilmentEntityGroupRequest>> UpdateAilmentEntityGroup(UpdateAilmentEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteAilmentEntityGroupRequest>> DeleteAilmentEntityGroup(DeleteAilmentEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<AilmentTagResponse>> GetAilmentTag(GetAilmentTagRequest request)
    {
        return await SendAsync<GetAilmentTagRequest, AilmentTagResponse>(request);
    }

    public async Task<QueryResponse<List<AilmentTagResponse>>> GetAilmentTagList(GetAilmentTagListRequest request)
    {
        return await SendAsync<GetAilmentTagListRequest, List<AilmentTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateAilmentTagRequest>> CreateAilmentTag(CreateAilmentTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateAilmentTagRequest>> UpdateAilmentTag(UpdateAilmentTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteAilmentTagRequest>> DeleteAilmentTag(DeleteAilmentTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MetaDataEntityGroupResponse>> GetMetaDataEntityGroup(GetMetaDataEntityGroupRequest request)
    {
        return await SendAsync<GetMetaDataEntityGroupRequest, MetaDataEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<MetaDataEntityGroupResponse>>> GetMetaDataEntityGroupList(GetMetaDataEntityGroupListRequest request)
    {
        return await SendAsync<GetMetaDataEntityGroupListRequest, List<MetaDataEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateMetaDataEntityGroupRequest>> CreateMetaDataEntityGroup(CreateMetaDataEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMetaDataEntityGroupRequest>> UpdateMetaDataEntityGroup(UpdateMetaDataEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMetaDataEntityGroupRequest>> DeleteMetaDataEntityGroup(DeleteMetaDataEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MetaDataEntityResponse>> GetMetaDataEntity(GetMetaDataEntityRequest request)
    {
        return await SendAsync<GetMetaDataEntityRequest, MetaDataEntityResponse>(request);
    }

    public async Task<QueryResponse<List<MetaDataEntityResponse>>> GetMetaDataEntityList(GetMetaDataEntityListRequest request)
    {
        return await SendAsync<GetMetaDataEntityListRequest, List<MetaDataEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateMetaDataEntityRequest>> CreateMetaDataEntity(CreateMetaDataEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMetaDataEntityRequest>> UpdateMetaDataEntity(UpdateMetaDataEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMetaDataEntityRequest>> DeleteMetaDataEntity(DeleteMetaDataEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MetaDatumResponse>> GetMetaDatum(GetMetaDatumRequest request)
    {
        return await SendAsync<GetMetaDatumRequest, MetaDatumResponse>(request);
    }

    public async Task<QueryResponse<List<MetaDatumResponse>>> GetMetaDatumList(GetMetaDatumListRequest request)
    {
        return await SendAsync<GetMetaDatumListRequest, List<MetaDatumResponse>>(request);
    }

    public async Task<CmdResponse<CreateMetaDatumRequest>> CreateMetaDatum(CreateMetaDatumRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMetaDatumRequest>> UpdateMetaDatum(UpdateMetaDatumRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMetaDatumRequest>> DeleteMetaDatum(DeleteMetaDatumRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ScheduleResponse>> GetSchedule(GetScheduleRequest request)
    {
        return await SendAsync<GetScheduleRequest, ScheduleResponse>(request);
    }

    public async Task<QueryResponse<List<ScheduleResponse>>> GetScheduleList(GetScheduleListRequest request)
    {
        return await SendAsync<GetScheduleListRequest, List<ScheduleResponse>>(request);
    }

    public async Task<CmdResponse<CreateScheduleRequest>> CreateSchedule(CreateScheduleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateScheduleRequest>> UpdateSchedule(UpdateScheduleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteScheduleRequest>> DeleteSchedule(DeleteScheduleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<UnitResponse>> GetUnit(GetUnitRequest request)
    {
        return await SendAsync<GetUnitRequest, UnitResponse>(request);
    }

    public async Task<QueryResponse<List<UnitResponse>>> GetUnitList(GetUnitListRequest request)
    {
        return await SendAsync<GetUnitListRequest, List<UnitResponse>>(request);
    }

    public async Task<CmdResponse<CreateUnitRequest>> CreateUnit(CreateUnitRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateUnitRequest>> UpdateUnit(UpdateUnitRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteUnitRequest>> DeleteUnit(DeleteUnitRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<UnitEntityResponse>> GetUnitEntity(GetUnitEntityRequest request)
    {
        return await SendAsync<GetUnitEntityRequest, UnitEntityResponse>(request);
    }

    public async Task<QueryResponse<List<UnitEntityResponse>>> GetUnitEntityList(GetUnitEntityListRequest request)
    {
        return await SendAsync<GetUnitEntityListRequest, List<UnitEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateUnitEntityRequest>> CreateUnitEntity(CreateUnitEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateUnitEntityRequest>> UpdateUnitEntity(UpdateUnitEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteUnitEntityRequest>> DeleteUnitEntity(DeleteUnitEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<UnitEntityGroupResponse>> GetUnitEntityGroup(GetUnitEntityGroupRequest request)
    {
        return await SendAsync<GetUnitEntityGroupRequest, UnitEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<UnitEntityGroupResponse>>> GetUnitEntityGroupList(GetUnitEntityGroupListRequest request)
    {
        return await SendAsync<GetUnitEntityGroupListRequest, List<UnitEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateUnitEntityGroupRequest>> CreateUnitEntityGroup(CreateUnitEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateUnitEntityGroupRequest>> UpdateUnitEntityGroup(UpdateUnitEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteUnitEntityGroupRequest>> DeleteUnitEntityGroup(DeleteUnitEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLogisticRider(VerifyLogisticRiderRequest request)
    {
        return await SendAsync<VerifyLogisticRiderRequest, IdentityValidationResponse>(request);
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
        return await SendAsync<GetPharmacyMemberListRequest, List<PharmacyMemberResponse>>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPharmacyMember(VerifyPharmacyMemberRequest request)
    {
        return await SendAsync<VerifyPharmacyMemberRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<UpdatePharmacyMemberRequest>> UpdatePharmacyMember(UpdatePharmacyMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyMemberRequest>> DeletePharmacyMember(DeletePharmacyMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyLocationResponse>> GetPharmacyLocation(GetPharmacyLocationRequest request)
    {
        return await SendAsync<GetPharmacyLocationRequest, PharmacyLocationResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyLocationResponse>>> GetPharmacyLocationList(GetPharmacyLocationListRequest request)
    {
        return await SendAsync<GetPharmacyLocationListRequest, List<PharmacyLocationResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyLocationRequest>> CreatePharmacyLocation(CreatePharmacyLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyLocationRequest>> UpdatePharmacyLocation(UpdatePharmacyLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyLocationRequest>> DeletePharmacyLocation(DeletePharmacyLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<StorageFileResponse>> GetPharmacyLocationDocument(GetPharmacyLocationDocumentRequest request)
    {
        return await SendAsync<GetPharmacyLocationDocumentRequest, StorageFileResponse>(request);
    }

    public async Task<QueryResponse<List<StorageFileResponse>>> GetPharmacyLocationDocumentList(GetPharmacyLocationDocumentListRequest request)
    {
        return await SendAsync<GetPharmacyLocationDocumentListRequest, List<StorageFileResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyLocationDocumentRequest>> CreatePharmacyLocationDocument(CreatePharmacyLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyLocationDocumentRequest>> UpdatePharmacyLocationDocument(UpdatePharmacyLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyLocationDocumentRequest>> DeletePharmacyLocationDocument(DeletePharmacyLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyJobOrderResponse>> GetPharmacyJobOrder(GetPharmacyJobOrderRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderRequest, PharmacyJobOrderResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyJobOrderResponse>>> GetPharmacyJobOrderList(GetPharmacyJobOrderListRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderListRequest, List<PharmacyJobOrderResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderRequest>> CreatePharmacyJobOrder(CreatePharmacyJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderRequest>> UpdatePharmacyJobOrder(UpdatePharmacyJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderRequest>> DeletePharmacyJobOrder(DeletePharmacyJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyEntityResponse>> GetPharmacyEntity(GetPharmacyEntityRequest request)
    {
        return await SendAsync<GetPharmacyEntityRequest, PharmacyEntityResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyEntityResponse>>> GetPharmacyEntityList(GetPharmacyEntityListRequest request)
    {
        return await SendAsync<GetPharmacyEntityListRequest, List<PharmacyEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyEntityRequest>> CreatePharmacyEntity(CreatePharmacyEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyEntityRequest>> UpdatePharmacyEntity(UpdatePharmacyEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyEntityRequest>> DeletePharmacyEntity(DeletePharmacyEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>> GetPharmacyJobOrderConsultationJobOrder(GetPharmacyJobOrderConsultationJobOrderRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderConsultationJobOrderRequest, PharmacyJobOrderConsultationJobOrderResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>> GetPharmacyJobOrderConsultationJobOrderList(GetPharmacyJobOrderConsultationJobOrderListRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderConsultationJobOrderListRequest, List<PharmacyJobOrderConsultationJobOrderResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderConsultationJobOrderRequest>> CreatePharmacyJobOrderConsultationJobOrder(CreatePharmacyJobOrderConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderRequest>> UpdatePharmacyJobOrderConsultationJobOrder(UpdatePharmacyJobOrderConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderConsultationJobOrderRequest>> DeletePharmacyJobOrderConsultationJobOrder(DeletePharmacyJobOrderConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyJobOrderMedicineResponse>> GetPharmacyJobOrderMedicine(GetPharmacyJobOrderMedicineRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderMedicineRequest, PharmacyJobOrderMedicineResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyJobOrderMedicineResponse>>> GetPharmacyJobOrderMedicineList(GetPharmacyJobOrderMedicineListRequest request)
    {
        return await SendAsync<GetPharmacyJobOrderMedicineListRequest, List<PharmacyJobOrderMedicineResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderMedicineRequest>> CreatePharmacyJobOrderMedicine(CreatePharmacyJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderMedicineRequest>> UpdatePharmacyJobOrderMedicine(UpdatePharmacyJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderMedicineRequest>> DeletePharmacyJobOrderMedicine(DeletePharmacyJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyServiceEntityGroupResponse>> GetPharmacyServiceEntityGroup(GetPharmacyServiceEntityGroupRequest request)
    {
        return await SendAsync<GetPharmacyServiceEntityGroupRequest, PharmacyServiceEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyServiceEntityGroupResponse>>> GetPharmacyServiceEntityGroupList(GetPharmacyServiceEntityGroupListRequest request)
    {
        return await SendAsync<GetPharmacyServiceEntityGroupListRequest, List<PharmacyServiceEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityGroupRequest>> CreatePharmacyServiceEntityGroup(CreatePharmacyServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyServiceEntityGroupRequest>> UpdatePharmacyServiceEntityGroup(UpdatePharmacyServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityGroupRequest>> DeletePharmacyServiceEntityGroup(DeletePharmacyServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyServiceEntityResponse>> GetPharmacyServiceEntity(GetPharmacyServiceEntityRequest request)
    {
        return await SendAsync<GetPharmacyServiceEntityRequest, PharmacyServiceEntityResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyServiceEntityResponse>>> GetPharmacyServiceEntityList(GetPharmacyServiceEntityListRequest request)
    {
        return await SendAsync<GetPharmacyServiceEntityListRequest, List<PharmacyServiceEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityRequest>> CreatePharmacyServiceEntity(CreatePharmacyServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyServiceEntityRequest>> UpdatePharmacyServiceEntity(UpdatePharmacyServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityRequest>> DeletePharmacyServiceEntity(DeletePharmacyServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyServiceResponse>> GetPharmacyService(GetPharmacyServiceRequest request)
    {
        return await SendAsync<GetPharmacyServiceRequest, PharmacyServiceResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyServiceResponse>>> GetPharmacyServiceList(GetPharmacyServiceListRequest request)
    {
        return await SendAsync<GetPharmacyServiceListRequest, List<PharmacyServiceResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyServiceRequest>> CreatePharmacyService(CreatePharmacyServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyServiceRequest>> UpdatePharmacyService(UpdatePharmacyServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyServiceRequest>> DeletePharmacyService(DeletePharmacyServiceRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyServiceTagResponse>> GetPharmacyServiceTag(GetPharmacyServiceTagRequest request)
    {
        return await SendAsync<GetPharmacyServiceTagRequest, PharmacyServiceTagResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyServiceTagResponse>>> GetPharmacyServiceTagList(GetPharmacyServiceTagListRequest request)
    {
        return await SendAsync<GetPharmacyServiceTagListRequest, List<PharmacyServiceTagResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyServiceTagRequest>> CreatePharmacyServiceTag(CreatePharmacyServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyServiceTagRequest>> UpdatePharmacyServiceTag(UpdatePharmacyServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyServiceTagRequest>> DeletePharmacyServiceTag(DeletePharmacyServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyStockResponse>> GetPharmacyStock(GetPharmacyStockRequest request)
    {
        return await SendAsync<GetPharmacyStockRequest, PharmacyStockResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyStockResponse>>> GetPharmacyStockList(GetPharmacyStockListRequest request)
    {
        return await SendAsync<GetPharmacyStockListRequest, List<PharmacyStockResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyStockRequest>> CreatePharmacyStock(CreatePharmacyStockRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyStockRequest>> UpdatePharmacyStock(UpdatePharmacyStockRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyStockRequest>> DeletePharmacyStock(DeletePharmacyStockRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyTagResponse>> GetPharmacyTag(GetPharmacyTagRequest request)
    {
        return await SendAsync<GetPharmacyTagRequest, PharmacyTagResponse>(request);
    }

    public async Task<QueryResponse<List<PharmacyTagResponse>>> GetPharmacyTagList(GetPharmacyTagListRequest request)
    {
        return await SendAsync<GetPharmacyTagListRequest, List<PharmacyTagResponse>>(request);
    }

    public async Task<CmdResponse<CreatePharmacyTagRequest>> CreatePharmacyTag(CreatePharmacyTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePharmacyTagRequest>> UpdatePharmacyTag(UpdatePharmacyTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeletePharmacyTagRequest>> DeletePharmacyTag(DeletePharmacyTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyPatient(VerifyPatientRequest request)
    {
        return await SendAsync<VerifyPatientRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<DeleteLogisticRequest>> DeleteLogistic(DeleteLogisticRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LogisticRiderHandleResponse>> GetLogisticRiderHandle(GetLogisticRiderHandleRequest request)
    {
        return await SendAsync<GetLogisticRiderHandleRequest, LogisticRiderHandleResponse>(request);
    }

    public async Task<QueryResponse<List<LogisticRiderHandleResponse>>> GetLogisticRiderHandleList(GetLogisticRiderHandleListRequest request)
    {
        return await SendAsync<GetLogisticRiderHandleListRequest, List<LogisticRiderHandleResponse>>(request);
    }

    public async Task<QueryResponse<IdentityValidationResponse>> VerifyLaboratoryMember(VerifyLaboratoryMemberRequest request)
    {
        return await SendAsync<VerifyLaboratoryMemberRequest, IdentityValidationResponse>(request);
    }

    public async Task<CmdResponse<DeleteLogisticRiderRequest>> DeleteLogisticRider(DeleteLogisticRiderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryResponse>> GetLaboratory(GetLaboratoryRequest request)
    {
        return await SendAsync<GetLaboratoryRequest, LaboratoryResponse>(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryMemberRequest>> DeleteLaboratoryMember(DeleteLaboratoryMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<List<LaboratoryServiceResponse>>> GetLaboratoryServiceList(GetLaboratoryServiceListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceListRequest, List<LaboratoryServiceResponse>>(request);
    }

    public async Task<QueryResponse<LaboratoryServiceResponse>> GetLaboratoryService(GetLaboratoryServiceRequest request)
    {
        return await SendAsync<GetLaboratoryServiceRequest, LaboratoryServiceResponse>(request);
    }

    public async Task<QueryResponse<ConsultationResponse>> GetConsultation(GetConsultationRequest request)
    {
        return await SendAsync<GetConsultationRequest, ConsultationResponse>(request);
    }

    public async Task<CmdResponse<CreateConsultationRequest>> CreateConsultation(CreateConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationRequest>> UpdateConsultation(UpdateConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationRequest>> DeleteConsultation(DeleteConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationEntityGroupResponse>> GetConsultationEntityGroup(GetConsultationEntityGroupRequest request)
    {
        return await SendAsync<GetConsultationEntityGroupRequest, ConsultationEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationEntityGroupResponse>>> GetConsultationEntityGroupList(GetConsultationEntityGroupListRequest request)
    {
        return await SendAsync<GetConsultationEntityGroupListRequest, List<ConsultationEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationEntityGroupRequest>> CreateConsultationEntityGroup(CreateConsultationEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationEntityGroupRequest>> UpdateConsultationEntityGroup(UpdateConsultationEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationEntityGroupRequest>> DeleteConsultationEntityGroup(DeleteConsultationEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationJobOrderLaboratoryResponse>> GetConsultationJobOrderLaboratory(GetConsultationJobOrderLaboratoryRequest request)
    {
        return await SendAsync<GetConsultationJobOrderLaboratoryRequest, ConsultationJobOrderLaboratoryResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationJobOrderLaboratoryResponse>>> GetConsultationJobOrderLaboratoryList(GetConsultationJobOrderLaboratoryListRequest request)
    {
        return await SendAsync<GetConsultationJobOrderLaboratoryListRequest, List<ConsultationJobOrderLaboratoryResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationJobOrderLaboratoryRequest>> CreateConsultationJobOrderLaboratory(CreateConsultationJobOrderLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationJobOrderLaboratoryRequest>> UpdateConsultationJobOrderLaboratory(UpdateConsultationJobOrderLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationJobOrderLaboratoryRequest>> DeleteConsultationJobOrderLaboratory(DeleteConsultationJobOrderLaboratoryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationJobOrderResponse>> GetConsultationJobOrder(GetConsultationJobOrderRequest request)
    {
        return await SendAsync<GetConsultationJobOrderRequest, ConsultationJobOrderResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationJobOrderResponse>>> GetConsultationJobOrderList(GetConsultationJobOrderListRequest request)
    {
        return await SendAsync<GetConsultationJobOrderListRequest, List<ConsultationJobOrderResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationJobOrderRequest>> CreateConsultationJobOrder(CreateConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationJobOrderRequest>> UpdateConsultationJobOrder(UpdateConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationJobOrderRequest>> DeleteConsultationJobOrder(DeleteConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationJobOrderMedicineResponse>> GetConsultationJobOrderMedicine(GetConsultationJobOrderMedicineRequest request)
    {
        return await SendAsync<GetConsultationJobOrderMedicineRequest, ConsultationJobOrderMedicineResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationJobOrderMedicineResponse>>> GetConsultationJobOrderMedicineList(GetConsultationJobOrderMedicineListRequest request)
    {
        return await SendAsync<GetConsultationJobOrderMedicineListRequest, List<ConsultationJobOrderMedicineResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationJobOrderMedicineRequest>> CreateConsultationJobOrderMedicine(CreateConsultationJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationJobOrderMedicineRequest>> UpdateConsultationJobOrderMedicine(UpdateConsultationJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationJobOrderMedicineRequest>> DeleteConsultationJobOrderMedicine(DeleteConsultationJobOrderMedicineRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationTagResponse>> GetConsultationTag(GetConsultationTagRequest request)
    {
        return await SendAsync<GetConsultationTagRequest, ConsultationTagResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationTagResponse>>> GetConsultationTagList(GetConsultationTagListRequest request)
    {
        return await SendAsync<GetConsultationTagListRequest, List<ConsultationTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateConsultationTagRequest>> CreateConsultationTag(CreateConsultationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateConsultationTagRequest>> UpdateConsultationTag(UpdateConsultationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateConsultationEntityRequest>> CreateConsultationEntity(CreateConsultationEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorRequest>> DeleteDoctor(DeleteDoctorRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<DoctorConsultationResponse>> GetDoctorConsultation(GetDoctorConsultationRequest request)
    {
        return await SendAsync<GetDoctorConsultationRequest, DoctorConsultationResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorConsultationResponse>>> GetDoctorConsultationList(GetDoctorConsultationListRequest request)
    {
        return await SendAsync<GetDoctorConsultationListRequest, List<DoctorConsultationResponse>>(request);
    }

    public async Task<CmdResponse<CreateDoctorConsultationRequest>> CreateDoctorConsultation(CreateDoctorConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateDoctorConsultationRequest>> UpdateDoctorConsultation(UpdateDoctorConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorConsultationRequest>> DeleteDoctorConsultation(DeleteDoctorConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<DoctorConsultationJobOrderResponse>> GetDoctorConsultationJobOrder(GetDoctorConsultationJobOrderRequest request)
    {
        return await SendAsync<GetDoctorConsultationJobOrderRequest, DoctorConsultationJobOrderResponse>(request);
    }


    public async Task<QueryResponse<List<DoctorConsultationJobOrderResponse>>> GetDoctorConsultationJobOrderList(GetDoctorConsultationJobOrderListRequest request)
    {
        return await SendAsync<GetDoctorConsultationJobOrderListRequest, List<DoctorConsultationJobOrderResponse>>(request);
    }

    public async Task<CmdResponse<CreateDoctorConsultationJobOrderRequest>> CreateDoctorConsultationJobOrder(CreateDoctorConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateDoctorConsultationJobOrderRequest>> UpdateDoctorConsultationJobOrder(UpdateDoctorConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorConsultationJobOrderRequest>> DeleteDoctorConsultationJobOrder(DeleteDoctorConsultationJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<DoctorEntityResponse>> GetDoctorEntity(GetDoctorEntityRequest request)
    {
        return await SendAsync<GetDoctorEntityRequest, DoctorEntityResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorEntityResponse>>> GetDoctorEntityList(GetDoctorEntityListRequest request)
    {
        return await SendAsync<GetDoctorEntityListRequest, List<DoctorEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateDoctorEntityRequest>> CreateDoctorEntity(CreateDoctorEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateDoctorEntityRequest>> UpdateDoctorEntity(UpdateDoctorEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorEntityRequest>> DeleteDoctorEntity(DeleteDoctorEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<DoctorEntityGroupResponse>> GetDoctorEntityGroup(GetDoctorEntityGroupRequest request)
    {
        return await SendAsync<GetDoctorEntityGroupRequest, DoctorEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorEntityGroupResponse>>> GetDoctorEntityGroupList(GetDoctorEntityGroupListRequest request)
    {
        return await SendAsync<GetDoctorEntityGroupListRequest, List<DoctorEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateDoctorEntityGroupRequest>> CreateDoctorEntityGroup(CreateDoctorEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateDoctorEntityGroupRequest>> UpdateDoctorEntityGroup(UpdateDoctorEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorEntityGroupRequest>> DeleteDoctorEntityGroup(DeleteDoctorEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<DoctorTagResponse>> GetDoctorTag(GetDoctorTagRequest request)
    {
        return await SendAsync<GetDoctorTagRequest, DoctorTagResponse>(request);
    }

    public async Task<QueryResponse<List<DoctorTagResponse>>> GetDoctorTagList(GetDoctorTagListRequest request)
    {
        return await SendAsync<GetDoctorTagListRequest, List<DoctorTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateDoctorTagRequest>> CreateDoctorTag(CreateDoctorTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateDoctorTagRequest>> UpdateDoctorTag(UpdateDoctorTagRequest request)
  {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteDoctorTagRequest>> DeleteDoctorTag(DeleteDoctorTagRequest request)
    {
        return await SendAsync(request);
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
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryLocationResponse>> GetLaboratoryLocation(GetLaboratoryLocationRequest request)
    {
        return await SendAsync<GetLaboratoryLocationRequest, LaboratoryLocationResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryLocationResponse>>> GetLaboratoryLocationList(GetLaboratoryLocationListRequest request)
    {
        return await SendAsync<GetLaboratoryLocationListRequest, List<LaboratoryLocationResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryLocationRequest>> CreateLaboratoryLocation(CreateLaboratoryLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryLocationRequest>> UpdateLaboratoryLocation(UpdateLaboratoryLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryLocationRequest>> DeleteLaboratoryLocation(DeleteLaboratoryLocationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<StorageFileResponse>> GetLaboratoryLocationDocument(GetLaboratoryLocationDocumentRequest request)
    {
        return await SendAsync<GetLaboratoryLocationDocumentRequest, StorageFileResponse>(request);
    }

    public async Task<QueryResponse<List<StorageFileResponse>>> GetLaboratoryLocationDocumentList(GetLaboratoryLocationDocumentListRequest request)
    {
        return await SendAsync<GetLaboratoryLocationDocumentListRequest, List<StorageFileResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryLocationDocumentRequest>> CreateLaboratoryLocationDocument(CreateLaboratoryLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryLocationDocumentRequest>> UpdateLaboratoryLocationDocument(UpdateLaboratoryLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryLocationDocumentRequest>> DeleteLaboratoryLocationDocument(DeleteLaboratoryLocationDocumentRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryJobOrderResponse>> GetLaboratoryJobOrder(GetLaboratoryJobOrderRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderRequest, LaboratoryJobOrderResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResponse>>> GetLaboratoryJobOrderList(GetLaboratoryJobOrderListRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderListRequest, List<LaboratoryJobOrderResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryJobOrderRequest>> CreateLaboratoryJobOrder(CreateLaboratoryJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryJobOrderRequest>> UpdateLaboratoryJobOrder(UpdateLaboratoryJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryJobOrderRequest>> DeleteLaboratoryJobOrder(DeleteLaboratoryJobOrderRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryMemberResponse>> GetLaboratoryMember(GetLaboratoryMemberRequest request)
    {
        return await SendAsync<GetLaboratoryMemberRequest, LaboratoryMemberResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryMemberResponse>>> GetLaboratoryMemberList(GetLaboratoryMemberListRequest request)
    {
        return await SendAsync<GetLaboratoryMemberListRequest, List<LaboratoryMemberResponse>>(request);
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
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryServiceEntityResponse>> GetLaboratoryServiceEntity(GetLaboratoryServiceEntityRequest request)
    {
        return await SendAsync<GetLaboratoryServiceEntityRequest, LaboratoryServiceEntityResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> GetLaboratoryServiceEntityList(GetLaboratoryServiceEntityListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceEntityListRequest, List<LaboratoryServiceEntityResponse>>(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceEntityRequest>> UpdateLaboratoryServiceEntity(UpdateLaboratoryServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceEntityRequest>> DeleteLaboratoryServiceEntity(DeleteLaboratoryServiceEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryServiceEntityGroupResponse>> GetLaboratoryServiceEntityGroup(GetLaboratoryServiceEntityGroupRequest request)
    {
        return await SendAsync<GetLaboratoryServiceEntityGroupRequest, LaboratoryServiceEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> GetLaboratoryServiceEntityGroupList(GetLaboratoryServiceEntityGroupListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceEntityGroupListRequest, List<LaboratoryServiceEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceEntityGroupRequest>> UpdateLaboratoryServiceEntityGroup(UpdateLaboratoryServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceEntityGroupRequest>> DeleteLaboratoryServiceEntityGroup(DeleteLaboratoryServiceEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultResponse>> GetLaboratoryJobOrderResult(GetLaboratoryJobOrderResultRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderResultRequest, LaboratoryJobOrderResultResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultResponse>>> GetLaboratoryJobOrderResultList(GetLaboratoryJobOrderResultListRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderResultListRequest, List<LaboratoryJobOrderResultResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryJobOrderResultRequest>> CreateLaboratoryJobOrderResult(CreateLaboratoryJobOrderResultRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryJobOrderResultRequest>> UpdateLaboratoryJobOrderResult(UpdateLaboratoryJobOrderResultRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultRequest>> DeleteLaboratoryJobOrderResult(DeleteLaboratoryJobOrderResultRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultFileResponse>> GetLaboratoryJobOrderResultFile(GetLaboratoryJobOrderResultFileRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderResultFileRequest, LaboratoryJobOrderResultFileResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultFileResponse>>> GetLaboratoryJobOrderResultFileList(GetLaboratoryJobOrderResultFileListRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderResultFileListRequest, List<LaboratoryJobOrderResultFileResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryJobOrderResultFileRequest>> CreateLaboratoryJobOrderResultFile(CreateLaboratoryJobOrderResultFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryJobOrderResultFileRequest>> UpdateLaboratoryJobOrderResultFile(UpdateLaboratoryJobOrderResultFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultFileRequest>> DeleteLaboratoryJobOrderResultFile(DeleteLaboratoryJobOrderResultFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryJobOrderDetailResponse>> GetLaboratoryJobOrderDetail(GetLaboratoryJobOrderDetailRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderDetailRequest, LaboratoryJobOrderDetailResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderDetailResponse>>> GetLaboratoryJobOrderDetailList(GetLaboratoryJobOrderDetailListRequest request)
    {
        return await SendAsync<GetLaboratoryJobOrderDetailListRequest, List<LaboratoryJobOrderDetailResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryJobOrderDetailRequest>> CreateLaboratoryJobOrderDetail(CreateLaboratoryJobOrderDetailRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryJobOrderDetailRequest>> UpdateLaboratoryJobOrderDetail(UpdateLaboratoryJobOrderDetailRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryJobOrderDetailRequest>> DeleteLaboratoryJobOrderDetail(DeleteLaboratoryJobOrderDetailRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryLocationTagResponse>> GetLaboratoryLocationTag(GetLaboratoryLocationTagRequest request)
    {
        return await SendAsync<GetLaboratoryLocationTagRequest, LaboratoryLocationTagResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryLocationTagResponse>>> GetLaboratoryLocationTagList(GetLaboratoryLocationTagListRequest request)
    {
        return await SendAsync<GetLaboratoryLocationTagListRequest, List<LaboratoryLocationTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryLocationTagRequest>> CreateLaboratoryLocationTag(CreateLaboratoryLocationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryLocationTagRequest>> UpdateLaboratoryLocationTag(UpdateLaboratoryLocationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryLocationTagRequest>> DeleteLaboratoryLocationTag(DeleteLaboratoryLocationTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryServiceTagResponse>> GetLaboratoryServiceTag(GetLaboratoryServiceTagRequest request)
    {
        return await SendAsync<GetLaboratoryServiceTagRequest, LaboratoryServiceTagResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryServiceTagResponse>>> GetLaboratoryServiceTagList(GetLaboratoryServiceTagListRequest request)
    {
        return await SendAsync<GetLaboratoryServiceTagListRequest, List<LaboratoryServiceTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryServiceTagRequest>> CreateLaboratoryServiceTag(CreateLaboratoryServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryServiceTagRequest>> UpdateLaboratoryServiceTag(UpdateLaboratoryServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryServiceTagRequest>> DeleteLaboratoryServiceTag(DeleteLaboratoryServiceTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryTagResponse>> GetLaboratoryTag(GetLaboratoryTagRequest request)
    {
        return await SendAsync<GetLaboratoryTagRequest, LaboratoryTagResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryTagResponse>>> GetLaboratoryTagList(GetLaboratoryTagListRequest request)
    {
        return await SendAsync<GetLaboratoryTagListRequest, List<LaboratoryTagResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryTagRequest>> CreateLaboratoryTag(CreateLaboratoryTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryTagRequest>> UpdateLaboratoryTag(UpdateLaboratoryTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryTagRequest>> DeleteLaboratoryTag(DeleteLaboratoryTagRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryEntityResponse>> GetLaboratoryEntity(GetLaboratoryEntityRequest request)
    {
        return await SendAsync<GetLaboratoryEntityRequest, LaboratoryEntityResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryEntityResponse>>> GetLaboratoryEntityList(GetLaboratoryEntityListRequest request)
    {
        return await SendAsync<GetLaboratoryEntityListRequest, List<LaboratoryEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryEntityRequest>> CreateLaboratoryEntity(CreateLaboratoryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryEntityRequest>> UpdateLaboratoryEntity(UpdateLaboratoryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryEntityRequest>> DeleteLaboratoryEntity(DeleteLaboratoryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LaboratoryEntityGroupResponse>> GetLaboratoryEntityGroup(GetLaboratoryEntityGroupRequest request)
    {
        return await SendAsync<GetLaboratoryEntityGroupRequest, LaboratoryEntityGroupResponse>(request);
    }

    public async Task<QueryResponse<List<LaboratoryEntityGroupResponse>>> GetLaboratoryEntityGroupList(GetLaboratoryEntityGroupListRequest request)
    {
        return await SendAsync<GetLaboratoryEntityGroupListRequest, List<LaboratoryEntityGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateLaboratoryEntityGroupRequest>> CreateLaboratoryEntityGroup(CreateLaboratoryEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateLaboratoryEntityGroupRequest>> UpdateLaboratoryEntityGroup(UpdateLaboratoryEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteLaboratoryEntityGroupRequest>> DeleteLaboratoryEntityGroup(DeleteLaboratoryEntityGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<List<ConsultationEntityResponse>>> GetConsultationEntityList(GetConsultationEntityListRequest request)
    {
        return await SendAsync<GetConsultationEntityListRequest, List<ConsultationEntityResponse>>(request);
    }

    public async Task<QueryResponse<ConsultationEntityResponse>> GetConsultationEntity(GetConsultationEntityRequest request)
    {
        return await SendAsync<GetConsultationEntityRequest, ConsultationEntityResponse>(request);
    }

    public async Task<CmdResponse<UpdateConsultationEntityRequest>> UpdateConsultationEntity(UpdateConsultationEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteConsultationEntityRequest>> DeleteConsultationEntity(DeleteConsultationEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<ConsultationPaymentResponse>> GetConsultationPayment(GetConsultationPaymentRequest request)
    {
        return await SendAsync<GetConsultationPaymentRequest, ConsultationPaymentResponse>(request);
    }

    public async Task<QueryResponse<List<ConsultationPaymentResponse>>> GetConsultationPaymentList(GetConsultationPaymentListRequest request)
    {
        return await SendAsync<GetConsultationPaymentListRequest, List<ConsultationPaymentResponse>>(request);
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
        return await SendAsync<GetLogisticRequest, LogisticResponse>(request);
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
        return await SendAsync(request);
    }

    public async Task<QueryResponse<LogisticRiderResponse>> GetLogisticRider(GetLogisticRiderRequest request)
    {
        return await SendAsync<GetLogisticRiderRequest, LogisticRiderResponse>(request);
    }

    public async Task<QueryResponse<List<LogisticRiderResponse>>> GetLogisticRiderList(GetLogisticRiderListRequest request)
    {
        return await SendAsync<GetLogisticRiderListRequest, List<LogisticRiderResponse>>(request);
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

    public async Task<CmdResponse<DeletePatientConsultationRequest>> DeletePatientConsultation(DeletePatientConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<List<string>>> GetMedicineAutoComplete(GetMedicineAutoCompleteRequest request)
    {
        return await SendAsync<GetMedicineAutoCompleteRequest, List<string>>(request);
    }

    public async Task<QueryResponse<PharmacyResponse>> GetPharmacy(GetPharmacyRequest request)
    {
        return await SendAsync<GetPharmacyRequest, PharmacyResponse>(request);
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
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PharmacyMemberResponse>> GetPharmacyMember(GetPharmacyMemberRequest request)
    {
        return await SendAsync<GetPharmacyMemberRequest, PharmacyMemberResponse>(request);
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

    public async Task<CmdResponse<DeleteConsultationPaymentRequest>> DeleteConsultationPayment(DeleteConsultationPaymentRequest request)
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

    public async Task<CmdResponse<DeletePatientRequest>> DeletePatient(DeletePatientRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<PatientConsultationResponse>> GetPatientConsultation(GetPatientConsultationRequest request)
    {
        return await SendAsync<GetPatientConsultationRequest, PatientConsultationResponse>(request);
    }

    public async Task<QueryResponse<List<PatientConsultationResponse>>> GetPatientConsultationList(GetPatientConsultationListRequest request)
    {
        return await SendAsync<GetPatientConsultationListRequest, List<PatientConsultationResponse>>(request);
    }

    public async Task<CmdResponse<CreatePatientConsultationRequest>> CreatePatientConsultation(CreatePatientConsultationRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdatePatientConsultationRequest>> UpdatePatientConsultation(UpdatePatientConsultationRequest request)
    {
        return await SendAsync(request);
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
        return await SendAsync<GetPatientListRequest, List<PatientResponse>>(request);
    }
 
    public async Task<CmdResponse<UpdatePatientRequest>> UpdatePatient(UpdatePatientRequest request)
    {
        return await SendAsync(request);
    }
}   