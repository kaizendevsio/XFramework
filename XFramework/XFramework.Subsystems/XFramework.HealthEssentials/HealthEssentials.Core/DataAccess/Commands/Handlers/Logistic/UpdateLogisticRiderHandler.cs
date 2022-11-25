using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticRiderHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticRiderCmd, CmdResponse<UpdateLogisticRiderCmd>>
{
    public UpdateLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateLogisticRiderCmd>> Handle(UpdateLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        var existingLogisticRider = await _dataLayer.HealthEssentialsContext.LogisticRiders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLogisticRider == null)
        {
            return new()
            {
                Message = $"Logistic rider with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLogisticRider = request.Adapt(existingLogisticRider);

        if (request.CredentialGuid is not null)
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
            updatedLogisticRider.CredentialGuid = credential.Guid;
        }
        
        _dataLayer.HealthEssentialsContext.LogisticRiders.Update(updatedLogisticRider);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic rider with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}