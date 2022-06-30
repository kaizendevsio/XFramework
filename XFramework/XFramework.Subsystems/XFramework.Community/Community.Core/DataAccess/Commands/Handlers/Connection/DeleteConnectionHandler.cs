using Community.Core.DataAccess.Commands.Entity.Connection;
using Community.Core.DataAccess.Commands.Entity.Identity;

namespace Community.Core.DataAccess.Commands.Handlers.Connection;

public class DeleteConnectionHandler : CommandBaseHandler, IRequestHandler<DeleteConnectionCmd, CmdResponse>
{
    public DeleteConnectionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse> Handle(DeleteConnectionCmd request, CancellationToken cancellationToken)
    {
        var connection = await _dataLayer.CommunityConnections.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (connection == null)
        {
            return new ()
            {
                Message = $"Community connection with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        connection.IsDeleted = true;
        
        _dataLayer.CommunityConnections.Update(connection);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}