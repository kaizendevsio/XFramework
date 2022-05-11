using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorListHandler : QueryBaseHandler, IRequestHandler<GetDoctorListQuery, QueryResponse<List<DoctorResponse>>>
{
    public GetDoctorListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<DoctorResponse>>> Handle(GetDoctorListQuery request, CancellationToken cancellationToken)
    {
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .Take(request.PageSize)
            .OrderBy(i => i.Name)
            .ToListAsync(CancellationToken.None);

        if (!doctor.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Doctor Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Doctor Found",
            IsSuccess = true,
            Response = doctor.Adapt<List<DoctorResponse>>()
        };
    }
}