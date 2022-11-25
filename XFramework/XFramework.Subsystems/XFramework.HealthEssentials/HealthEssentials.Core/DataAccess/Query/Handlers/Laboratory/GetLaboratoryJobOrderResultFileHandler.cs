using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultFileHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultFileQuery, QueryResponse<LaboratoryJobOrderResultFileResponse>>
{
    public GetLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultFileResponse>> Handle(GetLaboratoryJobOrderResultFileQuery request, CancellationToken cancellationToken)
    {
        var resultFile = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResultFiles
            .Include(x => x.StorageFileId)
            .Include(x => x.LaboratoryJobOrderResult)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (resultFile is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No result file found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Result file found",
            Response = resultFile.Adapt<LaboratoryJobOrderResultFileResponse>()
        };
    }
}