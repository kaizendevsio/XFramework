using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.Api.Controllers
{
    public class XFrameworkControllerBase : ControllerBase
    {
        public IMapper _mapper;
        public IConfiguration _configuration;
        public IMediator _mediator;

        public bool RequestResult { get; set; }
        public static string RequestResponseString { get; set; }
    }
}
