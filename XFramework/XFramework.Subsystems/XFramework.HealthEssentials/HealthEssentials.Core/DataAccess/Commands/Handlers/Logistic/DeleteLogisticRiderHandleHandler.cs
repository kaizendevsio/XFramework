﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderHandleCmd, CmdResponse<DeleteLogisticRiderHandleCmd>>
{
    public DeleteLogisticRiderHandleHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLogisticRiderHandleCmd>> Handle(DeleteLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingRecord == null)
        {
            return new()
            {
                Message = $"Logistic rider handle with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic rider handle with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}