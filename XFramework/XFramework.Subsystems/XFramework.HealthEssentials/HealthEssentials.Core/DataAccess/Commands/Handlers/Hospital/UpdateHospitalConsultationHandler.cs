﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalConsultationCmd, CmdResponse<UpdateHospitalConsultationCmd>>
{
    public UpdateHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalConsultationCmd>> Handle(UpdateHospitalConsultationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}