using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateMedicineEntityGroupCmd, CmdResponse<CreateMedicineEntityGroupCmd>>
{
    public CreateMedicineEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineEntityGroupCmd>> Handle(CreateMedicineEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<MedicineTypeGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.MedicineEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Medicine Entity Group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}