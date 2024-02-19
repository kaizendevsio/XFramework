using HealthEssentials.Domain.Generics.Constants;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Extensions;
using TModel = HealthEssentials.Domain.Generics.Contracts.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Doctors;

public class GetDoctorListByConsultationIdHandler(
    DbContext dbContext,
    IIdentityServerServiceWrapper identityServerService,
    ITenantService tenantService,
    ILogger<GetDoctorListByConsultationIdHandler> logger,
    IHelperService helperService
)
    : IRequestHandler<GetDoctorListByConsultationIdRequest, QueryResponse<PaginatedResult<TModel>>>
{
    public async Task<QueryResponse<PaginatedResult<TModel>>> Handle(GetDoctorListByConsultationIdRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        IQueryable<TModel> query = dbContext.Set<TModel>();

        if (request.Filter != null && request.Filter.Any())
        {
            var expression = request.Filter.ToExpression<TModel>();
            query = query.Where(expression);
        }
        
        query = query.Where(i => i.DoctorConsultations.Any(x => x.ConsultationId == request.ConsultationId))
            .Include(i => i.DoctorConsultations)
            .Include(i => i.DoctorTags)
            .Include(i => i.DoctorConsultationJobOrders);

        query = query
            .AsSplitQuery()
            .AsNoTracking();

        var items = await query
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.IsDeleted == false)
            .OrderBy(i => i.CreatedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        items = helperService.RemoveCircularReference(items);
        
        var paginatedResult = new PaginatedResult<TModel>(items.Count, request.PageIndex, request.PageSize, items);
        
        return new QueryResponse<PaginatedResult<TModel>>
        {
            Response = paginatedResult
        };
    }
}