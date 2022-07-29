﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceCmd, CmdResponse<DeleteHospitalServiceCmd>>
{
    public DeleteHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceCmd>> Handle(DeleteHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}