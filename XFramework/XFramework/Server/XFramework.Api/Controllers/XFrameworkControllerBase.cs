using System.Security.Claims;
using System.Text.Json;
using IdentityServer.Domain.Generic.Enums;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;

namespace XFramework.Api.Controllers
{
    public class XFrameworkControllerBase : ControllerBase
    {
        public IConfiguration _configuration;
        public IMediator _mediator;

        public TResponse AsApiRequest<TResponse, TRequest> (TResponse request)
        {
            var response = request.Adapt<TResponse>();
            string cuid = User.FindFirstValue(ClaimTypes.Name);

            var propertyInfo = response.GetType().GetProperty("Cuid");
            if (propertyInfo is not null)
            {
                propertyInfo.SetValue(response,cuid);
            }

            return response;
        }

        public TResponse ValidateIdentityElevation<TResponse>(TResponse request)
        {
            var cuid = User.FindFirstValue(ClaimTypes.Name);
            var roleString = User.FindFirstValue(ClaimTypes.Role);
            var roleList = JsonSerializer.Deserialize<List<RoleEntity>>(roleString);

            if (roleList.Any(i => i == RoleEntity.Administrator)) return request;
            
            var propertyInfo = request.GetType().GetProperty("Cuid");
            propertyInfo?.SetValue(request, cuid);

            return request;
        }
        
    }
}
