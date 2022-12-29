﻿using Microsoft.AspNetCore.Mvc;

namespace HealthEssentials.Api.Controllers;

public class XFrameworkControllerBase : ControllerBase
{
    public IConfiguration _configuration;
    public IMediator _mediator;

    public bool RequestResult { get; set; }
    public static string RequestResponseString { get; set; }
}