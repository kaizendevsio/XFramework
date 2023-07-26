using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace StreamFlow.Stream.Controllers;

public class XFrameworkControllerBase : ControllerBase
{
    public IConfiguration _configuration;
    public IMediator _mediator;

    public bool RequestResult { get; set; }
    public static string RequestResponseString { get; set; }
}