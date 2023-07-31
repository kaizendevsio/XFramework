using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorHandler : QueryBaseHandler, IRequestHandler<GetDoctorQuery, QueryResponse<DoctorResponse>>
{
    public GetDoctorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<DoctorResponse>> Handle(GetDoctorQuery request, CancellationToken cancellationToken)
    {
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors
            .Where(i => i.Guid == $"{request.Guid}")
            .Include(i => i.Entity)
            .ThenInclude(i => i.Group)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (doctor is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"No doctor found with the given guid {request.Guid}",
                
            };
        }
        
        var response = doctor.Adapt<DoctorResponse>();
        response.Files = _dataLayer.XnelSystemsContext.StorageFiles
            .Where(i => i.IdentifierGuid == $"{response.Guid}")
            .AsNoTracking()
            .ToList()
            .Adapt<List<StorageFileResponse>>();
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            Response = response
        };        
    }
}