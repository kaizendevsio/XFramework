﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceTagCmd, CmdResponse<DeleteHospitalServiceTagCmd>>
{
    public DeleteHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceTagCmd>> Handle(DeleteHospitalServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}