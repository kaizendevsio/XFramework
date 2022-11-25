using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultFileListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultFileListQuery, QueryResponse<List<LaboratoryJobOrderResultFileResponse>>>
{
    public GetLaboratoryJobOrderResultFileListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultFileResponse>>> Handle(GetLaboratoryJobOrderResultFileListQuery request, CancellationToken cancellationToken)
    {
        var resultFile = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResultFiles
            .Include(x => x.StorageFileId)
            .Include(x => x.LaboratoryJobOrderResult)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        

        if (!resultFile.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = resultFile.Adapt<List<LaboratoryJobOrderResultFileResponse>>()
        };
    }
}