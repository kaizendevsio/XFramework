using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd, CmdResponseBO>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public CreateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper, IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
            IdentityServiceWrapper = identityServiceWrapper;
        }

        public async Task<CmdResponseBO> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
                 
            var uuid = Guid.NewGuid();
            var cuid = Guid.NewGuid();
            var req = request.Adapt<CreateIdentityRequest>();
            req.Uuid = uuid;
            
            var phoneContact = new CreateContactRequest()
            {
                RequestServer = request.RequestServer,
                ContactType = GenericContactType.Phone,
                Cuid = cuid,
                Value = request.PhoneNumber
            };

            var emailContact = new CreateContactRequest()
            {
                RequestServer = request.RequestServer,
                ContactType = GenericContactType.Email,
                Cuid = cuid,
                Value = request.Email
            };
            
            
            var checkIdentityExistence = await IdentityServiceWrapper.CheckIdentityExistence(request.Adapt<CheckIdentityExistenceRequest>());
            if (checkIdentityExistence.HttpStatusCode == HttpStatusCode.Conflict)
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Conflict,
                    Message = checkIdentityExistence.Message
                };
            }
            
            var checkCredentialExistence = await IdentityServiceWrapper.CheckCredentialExistence(request.Adapt<CheckCredentialExistenceRequest>());
            if (checkCredentialExistence.HttpStatusCode == HttpStatusCode.Conflict)
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Conflict,
                    Message = checkCredentialExistence.Message
                };
            }

            var checkPhoneContactExistence = await IdentityServiceWrapper.CheckContactExistence(phoneContact.Adapt<CheckContactExistenceRequest>());
            if (checkPhoneContactExistence.HttpStatusCode == HttpStatusCode.Conflict)
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Conflict,
                    Message = checkPhoneContactExistence.Message
                };
            }
            
            var checkEmailContactExistence = await IdentityServiceWrapper.CheckContactExistence(emailContact.Adapt<CheckContactExistenceRequest>());
            if (checkEmailContactExistence.HttpStatusCode == HttpStatusCode.Conflict)
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Conflict,
                    Message = checkEmailContactExistence.Message
                };
            }

            var response = await IdentityServiceWrapper.CreateIdentity(req);
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response.Adapt<CmdResponseBO>();
            }

            var req2 = request.Adapt<CreateCredentialRequest>();
            req2.Uid = uuid;
            req2.Cuid = cuid;
            
            var response2 = await IdentityServiceWrapper.CreateCredential(req2);
            if (response2.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response2.Adapt<CmdResponseBO>();
            }

            var response3 = await IdentityServiceWrapper.CreateContact(phoneContact);
            if (response3.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response3.Adapt<CmdResponseBO>();
            }
            
            var response4 = await IdentityServiceWrapper.CreateContact(emailContact);
            if (response3.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response4.Adapt<CmdResponseBO>();
            }
            
            // TODO: Temporary Code, for refactor
            var lmwWallet = await WalletServiceWrapper.GetWallet(new() {Code = "LMW"});
            await WalletServiceWrapper.CreateIdentityWallet(new()
            {
                Balance = 0m,
                WalletTypeId = lmwWallet.Response?.Id
            });
            
            var imwWallet = await WalletServiceWrapper.GetWallet(new() {Code = "IMW"});
            await WalletServiceWrapper.CreateIdentityWallet(new()
            {
                Balance = 0m,
                WalletTypeId = imwWallet.Response?.Id
            });
            
            var ldwWallet = await WalletServiceWrapper.GetWallet(new() {Code = "LDW"});
            await WalletServiceWrapper.CreateIdentityWallet(new()
            {
                Balance = 0m,
                WalletTypeId = ldwWallet.Response?.Id
            });
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}