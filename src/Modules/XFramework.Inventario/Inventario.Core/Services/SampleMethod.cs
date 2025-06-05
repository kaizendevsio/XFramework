using System.Net;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Inventario.Domain.Shared.Contracts.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;

namespace Inventario.Core.Services;

public class SampleMethod(
    DbContext appDbContext,
    ILogger<SampleMethod> logger,
    ITenantService tenantService
)
    : IRequestHandler<SampleMethodRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(SampleMethodRequest request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        return new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = ""
        };
    }
}