﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalLaboratoryCmd, CmdResponse<UpdateHospitalLaboratoryCmd>>
{
    public UpdateHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalLaboratoryCmd>> Handle(UpdateHospitalLaboratoryCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}