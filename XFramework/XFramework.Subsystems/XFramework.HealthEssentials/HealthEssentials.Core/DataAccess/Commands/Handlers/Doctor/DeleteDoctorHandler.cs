using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorCmd, CmdResponse<DeleteDoctorCmd>>
{
    public DeleteDoctorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteDoctorCmd>> Handle(DeleteDoctorCmd request, CancellationToken cancellationToken)
    {
        var existingDoctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDoctor.IsDeleted = true;
        existingDoctor.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDoctor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Doctor with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}