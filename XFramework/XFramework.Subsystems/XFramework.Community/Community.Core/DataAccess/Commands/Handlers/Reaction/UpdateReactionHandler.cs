using Community.Core.DataAccess.Commands.Entity.Reaction;

namespace Community.Core.DataAccess.Commands.Handlers.Reaction;

public class UpdateReactionHandler : CommandBaseHandler, IRequestHandler<DeleteReactionCmd, CmdResponse>
{
    public UpdateReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(DeleteReactionCmd request, CancellationToken cancellationToken)
    {
        var communityContentReaction = await _dataLayer.CommunityContentReactions.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (communityContentReaction == null)
        {
            return new ()
            {
                Message = $"Content reaction with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        communityContentReaction.IsDeleted = true;

        _dataLayer.CommunityContentReactions.Update(communityContentReaction);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}