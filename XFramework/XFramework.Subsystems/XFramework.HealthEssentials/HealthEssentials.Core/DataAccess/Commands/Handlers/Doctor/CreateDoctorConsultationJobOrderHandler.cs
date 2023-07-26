using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<CreateDoctorConsultationJobOrderCmd, CmdResponse<CreateDoctorConsultationJobOrderCmd>>
{
    public CreateDoctorConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateDoctorConsultationJobOrderCmd>> Handle(CreateDoctorConsultationJobOrderCmd request, CancellationToken cancellationToken)
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
        
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (consultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var doctorConsultationJobOrder = request.Adapt<DoctorConsultationJobOrder>();
        doctorConsultationJobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        doctorConsultationJobOrder.Doctor = doctor;
        doctorConsultationJobOrder.ConsultationJobOrder = consultationJobOrder;
        
        await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders.AddAsync(doctorConsultationJobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(doctorConsultationJobOrder.Guid);
        return new()
        {
            Message = $"Doctor Consultation Job Order with Guid {doctorConsultationJobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}