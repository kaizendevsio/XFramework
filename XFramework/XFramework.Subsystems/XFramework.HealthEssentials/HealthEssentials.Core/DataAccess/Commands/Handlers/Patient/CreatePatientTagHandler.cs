using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientTagHandler : CommandBaseHandler, IRequestHandler<CreatePatientTagCmd, CmdResponse<CreatePatientTagCmd>>
{
    public CreatePatientTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePatientTagCmd>> Handle(CreatePatientTagCmd request, CancellationToken cancellationToken)
    {
        var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
        if (patient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.PatientGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patientTag = request.Adapt<PatientTag>();
        patientTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        patientTag.Patient = patient;
        patientTag.Tag = tag;

        await _dataLayer.HealthEssentialsContext.PatientTags.AddAsync(patientTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(patientTag.Guid);
        return new()
        {
            Message = $"Patient Tag with Guid {patientTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}