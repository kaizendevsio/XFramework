﻿using System.Security.Claims;
using System.Text.Json;
using IdentityServer.Domain.Generic.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace XFramework.Api.Controllers
{
    public class XFrameworkControllerBase : ControllerBase
    {
        public IConfiguration _configuration;
        public IMediator _mediator;

        public TResponse AsApiRequest<TResponse, TRequest> (TResponse request)
        {
            var response = request.Adapt<TResponse>();
            string credentialGuid = User.FindFirstValue(ClaimTypes.Name);

            var propertyInfo = response.GetType().GetProperty("CredentialGuid");
            if (propertyInfo is not null)
            {
                propertyInfo.SetValue(response,credentialGuid);
            }

            return response;
        }

        public TResponse InjectAuthorization<TResponse>(TResponse request)
        {
            var credentialGuid = User.FindFirstValue(ClaimTypes.Name);
            var roleString = User.FindFirstValue(ClaimTypes.Role);
            var roleList = JsonSerializer.Deserialize<List<RoleEntity>>(roleString);

            if (roleList.Any(i => i == RoleEntity.Administrator)) return request;
            
            var propertyInfo = request.GetType().GetProperty("CredentialGuid");
            var requestValue = propertyInfo.GetValue(request);
            if (string.IsNullOrEmpty($"{requestValue}"))
            {
                propertyInfo?.SetValue(request, credentialGuid);
            }
            return request;
        }
        
    }
}
