namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity;

public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd,CmdResponseBO<CreateIdentityCmd>>
{
    public CreateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponseBO<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<TblIdentityInformation>();
        entity.Guid = string.IsNullOrEmpty(entity.Guid) 
            ? Guid.NewGuid().ToString() 
            : entity.Guid;
        entity.BirthDate = request.Dob;
            
        await _dataLayer.TblIdentityInformations.AddAsync(entity, cancellationToken);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

}