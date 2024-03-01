using HealthEssentials.Domain.Generics.Contracts;
using HealthEssentials.Domain.Generics.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Integration.Drivers;

public partial interface IHealthEssentialsServiceWrapper
{
    public Task<QueryResponse<List<IdentityCredential>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request);
    public Task<CmdResponse> CommenceLiveConsultation(CommenceLiveConsultationRequest request);
    public Task<CmdResponse> ConcludeLiveConsultation(ConcludeLiveConsultationRequest request);
    public Task<QueryResponse<PaginatedResult<Doctor>>> GetDoctorListByConsultationId(GetDoctorListByConsultationIdRequest idRequest);
    public Task<QueryResponse<List<Medicine>>> MedicineAutoComplete(MedicineAutoCompleteRequest request);
}

public partial record HealthEssentialsServiceWrapper
{
    public Task<QueryResponse<List<IdentityCredential>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request)
    {
        return SendAsync<GetPendingRegistrationCompletionListRequest, List<IdentityCredential>>(request);
    }

    public Task<CmdResponse> CommenceLiveConsultation(CommenceLiveConsultationRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ConcludeLiveConsultation(ConcludeLiveConsultationRequest request)
    {
        return SendVoidAsync(request);
    }

    public async Task<QueryResponse<PaginatedResult<Doctor>>> GetDoctorListByConsultationId(GetDoctorListByConsultationIdRequest idRequest)
    {
        return await SendAsync<GetDoctorListByConsultationIdRequest, PaginatedResult<Doctor>>(idRequest);
    }

    public async Task<QueryResponse<List<Medicine>>> MedicineAutoComplete(MedicineAutoCompleteRequest request)
    {
        return await SendAsync<MedicineAutoCompleteRequest, List<Medicine>>(request);
    }
}