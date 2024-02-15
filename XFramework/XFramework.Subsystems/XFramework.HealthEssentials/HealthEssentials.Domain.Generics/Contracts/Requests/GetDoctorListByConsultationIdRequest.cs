namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = GetDoctorListByConsultationIdRequest;
using TResponse = QueryResponse<PaginatedResult<Doctor>>;

public record GetDoctorListByConsultationIdRequest : QueryableRequest,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid ConsultationId { get; set; }
    public List<QueryFilter>? Filter { get; set; }
};
    
    