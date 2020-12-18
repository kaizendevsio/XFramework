using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.DataAccess.Commands.Entity.Application;
using XFramework.Core.DataAccess.Commands.Entity.Events;
using XFramework.Core.Interfaces;
using XFramework.Domain.BO;
using XFramework.Domain.DTO;
using XFramework.Domain.Enums;

namespace XFramework.Api.Controllers
{
    public class XFrameworkControllerBase : ControllerBase
    {
        public IMapper _mapper;
        public IConfiguration _configuration;
        public IMediator _mediator;


        public ApiResponseBO ApiResponse { get; set; }
        public bool RequestResult { get; set; }
        public static string RequestResponseString { get; set; }


      
    }
}
