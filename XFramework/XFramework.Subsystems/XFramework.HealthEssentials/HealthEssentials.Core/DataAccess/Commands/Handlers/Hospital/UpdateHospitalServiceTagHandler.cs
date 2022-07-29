﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceTagCmd, CmdResponse<UpdateHospitalServiceTagCmd>>
{
    public UpdateHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceTagCmd>> Handle(UpdateHospitalServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}