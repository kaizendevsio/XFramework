﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticCmd, CmdResponse<UpdateLogisticCmd>>
{
    public UpdateLogisticHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateLogisticCmd>> Handle(UpdateLogisticCmd request, CancellationToken cancellationToken)
    {
        var existingLogistic = await _dataLayer.HealthEssentialsContext.Logistics
            .FirstOrDefaultAsync(x => x.Guid ==$"{request.Guid}", CancellationToken.None);

        if (existingLogistic == null)
        {
            return new()
            {
                Message = $"Logistic with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var updateLogistic = request.Adapt(existingLogistic);
        updateLogistic.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updateLogistic.Status = (int) GenericStatusType.Pending;

        _dataLayer.HealthEssentialsContext.Logistics.Update(updateLogistic);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Logistic with Guid {updateLogistic.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updateLogistic.Guid)
            }
        };
    }
}