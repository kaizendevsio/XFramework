using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryCmd, CmdResponse<CreateLaboratoryCmd>>
{
    private readonly IHelperService _helperService;

    public CreateLaboratoryHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryCmd>> Handle(CreateLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.LaboratoryEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var laboratory = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Laboratory>();
        laboratory.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        laboratory.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Laboratories.AddAsync(laboratory, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory with Guid {laboratory.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(laboratory.Guid)
            }
        };
    }
}