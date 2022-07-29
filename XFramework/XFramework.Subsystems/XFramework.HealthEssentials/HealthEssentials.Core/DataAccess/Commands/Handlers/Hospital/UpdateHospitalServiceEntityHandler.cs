﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceEntityHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceEntityCmd, CmdResponse<UpdateHospitalServiceEntityCmd>>
{
    public UpdateHospitalServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceEntityCmd>> Handle(UpdateHospitalServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}