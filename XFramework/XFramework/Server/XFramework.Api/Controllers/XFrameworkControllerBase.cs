using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace XFramework.Api.Controllers
{
    public class XFrameworkControllerBase : ControllerBase
    {
        public IConfiguration _configuration;
        public IMediator _mediator;
    }
}
