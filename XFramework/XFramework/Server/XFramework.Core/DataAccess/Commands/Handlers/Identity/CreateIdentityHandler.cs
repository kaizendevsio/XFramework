
namespace XFramework.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public CreateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper, IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
            IdentityServiceWrapper = identityServiceWrapper;
        }

        public async Task<CmdResponse> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
            var uuid = Guid.NewGuid();
            var cuid = Guid.NewGuid();
            var req = request.Adapt<CreateIdentityRequest>();
            req.Guid = uuid;
            
            var phoneContact = new CreateContactRequest()
            {
                RequestServer = request.RequestServer,
                ContactType = GenericContactType.Phone,
                CredentialGuid = cuid,
                Value = request.PhoneNumber
            };

            var emailContact = new CreateContactRequest()
            {
                RequestServer = request.RequestServer,
                ContactType = GenericContactType.Email,
                CredentialGuid = cuid,
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
                return response.Adapt<CmdResponse>();
            }

            var req2 = request.Adapt<CreateCredentialRequest>();
            req2.IdentityGuid = uuid;
            req2.Guid = cuid;
            
            var response2 = await IdentityServiceWrapper.CreateCredential(req2);
            if (response2.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response2.Adapt<CmdResponse>();
            }

            var response3 = await IdentityServiceWrapper.CreateContact(phoneContact);
            if (response3.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response3.Adapt<CmdResponse>();
            }
            
            var response4 = await IdentityServiceWrapper.CreateContact(emailContact);
            if (response3.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response4.Adapt<CmdResponse>();
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}