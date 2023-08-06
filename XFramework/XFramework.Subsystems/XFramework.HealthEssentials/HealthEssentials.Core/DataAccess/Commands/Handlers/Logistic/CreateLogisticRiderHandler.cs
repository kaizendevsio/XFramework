using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticRiderHandler : CommandBaseHandler, IRequestHandler<CreateLogisticRiderCmd, CmdResponse<CreateLogisticRiderCmd>>
{
    public CreateLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticRiderCmd>> Handle(CreateLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
      
        var rider = request.Adapt<LogisticRider>();
        rider.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        rider.CredentialGuid = credential.Guid;
        rider.Status = (int) GenericStatusType.Pending;
        rider.LicenseExpiry = request.LicenseExpiry?.ToUniversalTime();

        await _dataLayer.HealthEssentialsContext.LogisticRiders.AddAsync(rider, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(rider.Guid);
        return new()
        {
            Message = $"Rider with guid {rider.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}