using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential
{
    public class GetIdentityCredentialListHandler : QueryBaseHandler, IRequestHandler<GetIdentityCredentialListQuery, QueryResponseBO<List<IdentityCredentialContract>>>
    {
        private readonly LegacyContext _legacyContext;

        public GetIdentityCredentialListHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService, JwtOptionsBO jwtOptionsBo, IJwtService jwtService, ILoggerWrapper recordsWrapper, LegacyContext legacyContext)
        {
            _legacyContext = legacyContext;
            _dataLayer = dataLayer;
            _cachingService = cachingService;
        }
        
        public async Task<QueryResponseBO<List<IdentityCredentialContract>>> Handle(GetIdentityCredentialListQuery request, CancellationToken cancellationToken)
        {
            List<TblIdentityCredential> result = new();
            request.ListCount = request.ListCount > 2000 ? 10000 : request.ListCount;
       
            if (request.Filter)
            {
                result = await _dataLayer.TblIdentityRoles
                    .Include(i => i.UserCred)
                    .ThenInclude(i => i.TblIdentityContacts)
                    .ThenInclude(i => i.Ucentities)
                    .Include(i => i.UserCred)
                    .ThenInclude(i => i.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => i.RoleEntityId == (decimal?)request.IdentityRole)
                    .Where(i => i.UserCred.ApplicationId == request.RequestServer.ApplicationId)
                    .Where(i => 
                        EF.Functions.Like(i.UserCred.IdentityInfo.FirstName,$"%{request.SearchString}%") ||
                        EF.Functions.Like(i.UserCred.IdentityInfo.MiddleName,$"%{request.SearchString}%") ||
                        EF.Functions.Like(i.UserCred.IdentityInfo.LastName,$"%{request.SearchString}%") ||
                        EF.Functions.Like(i.UserCred.UserName,$"%{request.SearchString}%") ||
                        i.UserCred.TblIdentityContacts.Any( o => EF.Functions.Like(o.Value,$"%{request.SearchString}%"))
                        )
                    .Skip(request.Skip)
                    .Take(request.ListCount)
                    .Select(i => i.UserCred)
                    .OrderBy(i => i.IdentityInfo.FirstName)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            else
            {
                result = await _dataLayer.TblIdentityRoles
                    .Include(i => i.UserCred.TblIdentityContacts)
                    .ThenInclude(i => i.Ucentities)
                    .Include(i => i.UserCred.IdentityInfo)
                    .Include(i => i.UserCred.TblIdentityRoles)
                    .AsNoTracking()
                    .Where(i => i.RoleEntityId != (decimal?)request.IdentityRole)
                    .Skip(request.Skip)
                    .Take(request.ListCount)
                    .Select(i => i.UserCred)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            
            if (!result.Any())
            {
                return new ()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = result.Adapt<List<IdentityCredentialContract>>()
            };
        }

        private async Task<List<TblIdentityCredential>> LegacyResult(GetIdentityCredentialListQuery request)
        {
            List<TblIdentityCredential> result;
            result = await _legacyContext.TblUserRole
                .AsNoTracking()
                .Include(i => i.UserAuth)
                .ThenInclude(i => i.UserInfo)
                .Include(i => i.UserAuth.TblUserRole)
                .Where(i => i.AccessRole == (request.IdentityRole == RoleEntity.Client ? "Client" : "Admin"))
                .Where(i =>
                    EF.Functions.Like(i.UserAuth.UserInfo.FirstName, $"%{request.SearchString}%") ||
                    EF.Functions.Like(i.UserAuth.UserInfo.LastName, $"%{request.SearchString}%") ||
                    EF.Functions.Like(i.UserAuth.UserName, $"%{request.SearchString}%") ||
                    EF.Functions.Like(i.UserAuth.UserInfo.PhoneNumber, $"%{request.SearchString}%") ||
                    EF.Functions.Like(i.UserAuth.UserInfo.Email, $"%{request.SearchString}%")
                )
                .Take(request.ListCount)
                .Select(i => i.UserAuth)
                .OrderBy(i => i.UserInfo.FirstName)
                .Select(c => new TblIdentityCredential()
                {
                    Id = c.Id,
                    Cuid = c.Id.ToString(),
                    UserName = c.UserName,
                    IdentityInfoId = c.UserInfoId,
                    TblIdentityRoles = new List<TblIdentityRole>()
                    {
                        new()
                        {
                            Id = c.TblUserRole.First().Id,
                            RoleEntityId = (long)RoleEntity.Client
                        }
                    },
                    IdentityInfo = new()
                    {
                        Id = c.Id,
                        FirstName = c.UserInfo.FirstName,
                        LastName = c.UserInfo.LastName,
                        BirthDate = c.UserInfo.Dob == null
                            ? DateTime.MinValue
                            : DateTime.Parse(c.UserInfo.Dob.ToString()),
                        Uuid = c.UserInfo.Uid,
                        Gender = c.UserInfo.Gender ?? (short)Gender.NotSpecified
                    },
                    TblIdentityContacts = new List<TblIdentityContact>()
                    {
                        new()
                        {
                            Id = c.Id,
                            UcentitiesId = (long)GenericContactType.Phone,
                            Value = c.UserInfo.PhoneNumber,
                        },
                        new()
                        {
                            Id = c.Id,
                            UcentitiesId = (long)GenericContactType.Email,
                            Value = c.UserInfo.Email,
                        }
                    }
                })
                .ToListAsync();
            return result;
        }
    }
}