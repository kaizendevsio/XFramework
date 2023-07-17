﻿using Community.Core.DataAccess.Commands.Entity.Reaction;

namespace Community.Core.DataAccess.Commands.Handlers.Reaction;

public class CreateReactionHandler : CommandBaseHandler, IRequestHandler<CreateReactionCmd, CmdResponse>
{
    public CreateReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateReactionCmd request, CancellationToken cancellationToken)
    {
        var communityIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityGuid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.CommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        var content = await _dataLayer.CommunityContents.FirstOrDefaultAsync(i => i.Guid == $"{request.ContentGuid}", cancellationToken: cancellationToken);
        if (content == null)
        {
            return new ()
            {
                Message = $"Content with guid {request.ContentGuid} does not exist",
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

        var reaction = new CommunityContentReaction
        {
            Content = content,
            Entity = reactionEntity,
            SocialMediaIdentity = communityIdentity
        };

        _dataLayer.CommunityContentReactions.Add(reaction);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}