namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity;

public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd,CmdResponse<CreateIdentityCmd>>
{
    public CreateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        
        var entity = request.Adapt<IdentityInformation>();
        entity.Guid = string.IsNullOrEmpty(entity.Guid) 
            ? Guid.NewGuid().ToString() 
            : $"{request.Guid}";
        entity.BirthDate = request.Dob is null ? null : DateOnly.FromDateTime(request.Dob.Value);
        entity.Application = application;
            
        await _dataLayer.IdentityInformations.AddAsync(entity, cancellationToken);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

}