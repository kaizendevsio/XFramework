using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorIdentityCmd, CmdResponse<UpdateDoctorIdentityCmd>>
{
    public UpdateDoctorIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateDoctorIdentityCmd>> Handle(UpdateDoctorIdentityCmd request, CancellationToken cancellationToken)
    {
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (doctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        doctor.Status = (int) request.Status;
        _dataLayer.HealthEssentialsContext.Update(doctor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Doctor Identity Updated Successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}