using Community.Core.DataAccess.Commands.Entity.Reaction;

namespace Community.Core.DataAccess.Commands.Handlers.Reaction;

public class DeleteReactionHandler : CommandBaseHandler, IRequestHandler<UpdateReactionCmd, CmdResponse>
{
    public DeleteReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(UpdateReactionCmd request, CancellationToken cancellationToken)
    {
        var communityContentReaction = await _dataLayer.CommunityContentReactions.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (communityContentReaction == null)
        {
            return new ()
            {
                Message = $"Content reaction with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        var reactionEntity = await _dataLayer.CommunityContentReactionEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.ReactionEntityGuid}", cancellationToken: cancellationToken);
        if (reactionEntity == null)
        {
            return new ()
            {
                Message = $"Reaction entity with guid {request.ReactionEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        communityContentReaction.Entity = reactionEntity;

        _dataLayer.CommunityContentReactions.Update(communityContentReaction);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}