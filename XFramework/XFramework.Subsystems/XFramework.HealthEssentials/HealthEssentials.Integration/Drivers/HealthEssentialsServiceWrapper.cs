using HealthEssentials.Domain.Generics.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;

namespace HealthEssentials.Integration.Drivers;

public partial interface IHealthEssentialsServiceWrapper
{
    public Task<QueryResponse<List<IdentityCredential>>> GetPendingRegistrationCompletionList(GetPendingRegistrationCompletionListRequest request);
    public Task<CmdResponse> CommenceLiveConsultation(CommenceLiveConsultationRequest request);
    public Task<CmdResponse> ConcludeLiveConsultation(ConcludeLiveConsultationRequest request);
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
}