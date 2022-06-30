using Community.Core.DataAccess.Commands.Entity.Connection;

namespace Community.Core.DataAccess.Commands.Handlers.Connection;

public class CreateConnectionHandler : CommandBaseHandler, IRequestHandler<CreateConnectionCmd, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateConnectionCmd request, CancellationToken cancellationToken)
    {
        var sourceIdentity = await _dataLayer.CommunityIdentities
            
            .Where(i => i.Guid == $"{request.SourceCommunityIdentityGuid}")
            .FirstOrDefaultAsync(CancellationToken.None);
     
        if (sourceIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.SourceCommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var targetIdentity = await _dataLayer.CommunityIdentities
            
            .Where(i => i.Guid == $"{request.TargetCommunityIdentityGuid}")
            .FirstOrDefaultAsync(CancellationToken.None);
     
        if (targetIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.TargetCommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var connectionEntity = await _dataLayer.CommunityConnectionEntities
            
            .Where(i => i.Guid == $"{request.CommunityConnectionEntityGuid}")
            .FirstOrDefaultAsync(CancellationToken.None);
     
        if (connectionEntity == null)
        {
            return new ()
            {
                Message = $"Connection entity with guid {request.CommunityConnectionEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var newConnection = new CommunityConnection
        {
            Entity = connectionEntity,
            SourceSocialMediaIdentity = sourceIdentity,
            TargetSocialMediaIdentity = targetIdentity
        };

        _dataLayer.CommunityConnections.Add(newConnection);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}