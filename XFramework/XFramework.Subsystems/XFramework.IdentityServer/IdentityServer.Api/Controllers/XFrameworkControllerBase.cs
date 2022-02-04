using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers;

public class XFrameworkControllerBase : ControllerBase
{
    public IConfiguration _configuration;
    public IMediator _mediator;

    public bool RequestResult { get; set; }
    public static string RequestResponseString { get; set; }
}