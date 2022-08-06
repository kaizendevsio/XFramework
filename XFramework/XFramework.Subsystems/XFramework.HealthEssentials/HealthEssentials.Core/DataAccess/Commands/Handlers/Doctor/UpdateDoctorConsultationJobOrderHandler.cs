using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorConsultationJobOrderCmd, CmdResponse<UpdateDoctorConsultationJobOrderCmd>>
{
    public UpdateDoctorConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateDoctorConsultationJobOrderCmd>> Handle(UpdateDoctorConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingDoctorConsultationJobOrder = await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctorConsultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Doctor Consultation Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDoctorConsultationJobOrder = request.Adapt(existingDoctorConsultationJobOrder);

        if (request.DoctorGuid is null)
        {
            var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(x => x.Guid == $"{request.DoctorGuid}", CancellationToken.None);
            if (doctor is null)
            {
                return new ()
                {
                    Message = $"Doctor with Guid {request.DoctorGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorConsultationJobOrder.Doctor = doctor;
        }

        if (request.ConsultationJobOrderGuid is null)
        {
            var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
            if (consultationJobOrder is null)
            {
                return new ()
                {
                    Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorConsultationJobOrder.ConsultationJobOrder = consultationJobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDoctorConsultationJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Consultation with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}