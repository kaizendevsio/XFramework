using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using IdentityServer.Domain.Generic.Contracts.Responses;

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
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.Status)
            .Include(i => i.DoctorConsultations)
            .ThenInclude(i => i.Consultation)
            .Include(i => i.Entity)
            .ThenInclude(i => i.Group)
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        
        var mappedDoctors = doctor.Adapt<List<DoctorResponse>>();
        foreach (var item in mappedDoctors)
        {
           var a = await _dataLayer.XnelSystemsContext.IdentityCredentials
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Guid == item.CredentialId, CancellationToken.None);

           if (a is null) continue;
           
           item.Credential = a.Adapt<CredentialResponse>();
        };

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
            Response = mappedDoctors
        };
    }
}