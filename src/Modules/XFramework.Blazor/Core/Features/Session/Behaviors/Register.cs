﻿using System.Collections.Specialized;
using IdentityServer.Domain.Shared;
using IdentityServer.Integration.Drivers;
using XFramework.Blazor.Core.Features.Wallet;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Blazor.Core.Features.Session;
public partial class SessionState
{
    public record Register : NavigableRequest<CmdResponse<IdentityCredential?>>
    {
        public bool AutoLogin { get; set; } = true;
        public bool SkipVerification { get; set; }
        public List<Guid>? WalletList { get; set; }
        public List<Guid>? RoleList { get; set; }
    }
    
    protected class RegisterStateActionHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Register, CmdResponse<IdentityCredential?>>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task<CmdResponse<IdentityCredential?>> Handle(Register action, CancellationToken aCancellationToken)
        {
            CurrentState.RegisterVm.RoleList ??= action.RoleList;
            
            // Inform UI About Busy State
            await ReportBusy("Creating Account..", true);

            // Check If Any Given Data Are Already Existing
            if (await CheckDuplicateRecords(action))
                return new()
                {
                    HttpStatusCode = HttpStatusCode.PreconditionFailed,
                    Message = "Duplicate records found"
                };

            // Check If Passwords Are Correct
            if (!CurrentState.RegisterVm.Password.Equals(CurrentState.RegisterVm.PasswordConfirmation))
            {
                SweetAlertService.FireAsync("Password does not match",$"", SweetAlertIcon.Error);
                return new()
                {
                    HttpStatusCode = HttpStatusCode.PreconditionFailed,
                    Message = "Password does not match"
                };
            }
            
            // Create Guids
            var identityId = Guid.NewGuid();
            var credentialId = Guid.NewGuid();

            // Send Create Identity Request
            await ReportBusy("Creating identity..", null);
            var identityRequest = CurrentState.RegisterVm.Adapt<IdentityInformation>();
            identityRequest.Id = identityId;
            
            var identity = await identityServerServiceWrapper.IdentityInformation.Create(identityRequest);
            if (await HandleFailure(identity, action))
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = "Failed to create identity"
                };
            
            // Send Create Credential Request
            await ReportBusy("Creating credential..", null);
            var credentialRequest = CurrentState.RegisterVm.Adapt<IdentityCredential>();
            credentialRequest.Id = credentialId;
            credentialRequest.IdentityInfoId = identityId;
            
            var credential = await identityServerServiceWrapper.IdentityCredential.Create(credentialRequest);
            if (await HandleFailure(credential, action)) return
                new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = "Failed to create credential"
                };
            
            // Send Create Phone Contact Request
            await ReportBusy("Creating contacts..", null);

            var phoneContact = await CreateContact(
                action: action, 
                credentialId: credentialId, 
                contactTypeId: IdentityConstants.ContactType.Phone, 
                contactGroupId: IdentityConstants.ContactGroup.Personal, 
                contactValue: CurrentState.RegisterVm.PhoneNumber);

            if (await HandleFailure(phoneContact, action))
            {
                return phoneContact.Adapt<CmdResponse<IdentityCredential?>>();
            }
            
            var emailContact = await CreateContact(
                action: action, 
                credentialId: credentialId, 
                contactTypeId: IdentityConstants.ContactType.Email, 
                contactGroupId: IdentityConstants.ContactGroup.Personal, 
                contactValue: CurrentState.RegisterVm.EmailAddress);
            
            if (await HandleFailure(emailContact, action))
            {
                return emailContact.Adapt<CmdResponse<IdentityCredential?>>();
            }
            
            // If WalletList property is provided, automatically create wallets
            if (action.WalletList is not null)
            {
                await ReportBusy("Creating wallets..", null);
                await CreateWallets(action.WalletList, credentialId);
            }
            
            // If Roles property is provided, automatically create wallets
            if (action.RoleList is not null && action.RoleList.Any())
            {
                await ReportBusy("Creating roles..", null);
                await CreateRoles(CurrentState.RegisterVm.RoleList, credentialId);
            }
            
            // If AutoLogin property is true, automatically log the identity in
            if (action.AutoLogin)
            {
                ReportBusy("Logging In..", null);
                var username = string.Empty;
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
                {
                    username = CurrentState.RegisterVm.PhoneNumber;
                }
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
                {
                    username = CurrentState.RegisterVm.EmailAddress;
                }
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.UserName))
                {
                    username = CurrentState.RegisterVm.UserName;
                }

                SessionState.LoginVm.UserName = username; 
                SessionState.LoginVm.Password = CurrentState.RegisterVm.Password; 
                await Mediator.Send(new Login()
                {
                    NavigateToOnSuccess = action.NavigateToOnSuccess,
                    SkipVerification = action.SkipVerification,
                    Role = action.RoleList.First()
                });
            
                return new()
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Message = "Account created successfully",
                    Response = credential.Response
                };
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(credential, action, silent: action.Silent, customMessage:"Account created successfully");
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Account created successfully",
                Response = credential.Response
            };
        }

        private async Task<CmdResponse> CreateContact(Register action, Guid credentialId, Guid contactTypeId, Guid contactGroupId, string? contactValue)
        {
            var contactType = await identityServerServiceWrapper.IdentityContactType.GetList(
                pageSize: 2,
                pageNumber: 1,
                filter:
                [
                    new()
                    {
                        PropertyName = nameof(IdentityContactType.SystemReferenceId),
                        Operation = QueryFilterOperation.Equal,
                        Value = contactTypeId
                    }
                ]
            );
            
            if (contactType.IsSuccess is false)
            {
                return contactType.Adapt<CmdResponse>();
            }

            if (contactType.Response is null || contactType.Response.TotalItems == 0)
            {
                await SweetAlertService.FireAsync(new()
                {
                    Title = "Error",
                    Text = "Contact type not supported",
                    Icon = SweetAlertIcon.Error,
                    ShowCloseButton = true,
                    ConfirmButtonText = "Close",
                });
                
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = "Contact type not supported"
                };
            }
            
            var contactGroup = await identityServerServiceWrapper.IdentityContactGroup.GetList(
                pageNumber: 0,
                pageSize: 1,
                filter:
                [
                    new()
                    {
                        PropertyName = nameof(IdentityContactGroup.SystemReferenceId),
                        Operation = QueryFilterOperation.Equal,
                        Value = contactGroupId
                    }
                ]);

            if (contactGroup.IsSuccess is false)
            {
                return contactGroup.Adapt<CmdResponse>();
            }

            if (contactGroup.Response is null || contactGroup.Response.TotalItems == 0)
            {
                await SweetAlertService.FireAsync(new()
                {
                    Title = "Error",
                    Text = "Contact group not supported",
                    Icon = SweetAlertIcon.Error,
                    ShowCloseButton = true,
                    ConfirmButtonText = "Close",
                });
                
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = "Contact group not supported"
                };
            }
            
            var contact = await identityServerServiceWrapper.IdentityContact.Create(new(){
                CredentialId = credentialId,
                TypeId = contactType.Response.Items.First().Id,
                GroupId = contactGroup.Response.Items.First().Id,
                Value = !string.IsNullOrEmpty(contactValue) ? contactValue : string.Empty,
                /*SendOtp = !action.SkipVerification*/
            });
            
            if (contact.IsSuccess is false)
            {
                return contact.Adapt<CmdResponse>();
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Contact created successfully"
            };
        }

        private async Task CreateWallets(List<Guid> walletList, Guid credentialGuid)
        {
            foreach (var walletTypeGuid in walletList)
            {
                await Mediator.Send(new WalletState.CreateWallet
                {
                    CredentialId = credentialGuid,
                    WalletTypeId = walletTypeGuid,
                    Silent = true
                });
            }
        }
        
        private async Task CreateRoles(List<Guid>? roleList, Guid credentialGuid)
        {
            var tasks = roleList.Select(typeId =>
                    identityServerServiceWrapper.IdentityRole.Create(new()
                        {
                            CredentialId = credentialGuid,
                            TypeId = typeId,
                            IsEnabled = true
                        }
                    ))
                .Cast<Task>()
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task<bool> CheckDuplicateRecords(Register action)
        {
            // Check Credential Duplicates
            await ReportBusy("Validating credentials..", null);
            var credentialExistence = await identityServerServiceWrapper.IdentityCredential.GetList(pageSize:1, pageNumber:1, filter:
            [
                new()
                {
                    PropertyName = nameof(IdentityCredential.UserName),
                    Operation = QueryFilterOperation.Equal,
                    Value = CurrentState.RegisterVm.UserName
                }
            ]);
            if (await HandleFailure(credentialExistence, action)) return true;
            if (credentialExistence.Response.TotalItems > 0)
            {
                SweetAlertService.FireAsync("Username already exists",$"", SweetAlertIcon.Error);
                return true;
            }

            // Check Phone Number Duplicates
            await ReportBusy("Checking for duplicate phone numbers..", null);
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneContactExistence = await identityServerServiceWrapper.IdentityContact.GetList(pageSize:1, pageNumber:1, filter:
                [
                    new()
                    {
                        PropertyName = nameof(IdentityContact.Value),
                        Operation = QueryFilterOperation.Equal,
                        Value =  CurrentState.RegisterVm.PhoneNumber!.ValidatePhoneNumber()
                    }
                ]);
                
                if (await HandleFailure(phoneContactExistence, action)) return true;
                if (phoneContactExistence.Response.TotalItems > 0)
                {
                    SweetAlertService.FireAsync("Phone number already exists",$"", SweetAlertIcon.Error);
                    return true;
                }
            }

            // Check Email Address Duplicates
            await ReportBusy("Checking for duplicate email address..", null);
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
            {
                var emailContactExistence = await identityServerServiceWrapper.IdentityContact.GetList(pageSize:1, pageNumber:1, filter:
                [
                    new()
                    {
                        PropertyName = nameof(IdentityContact.Value),
                        Operation = QueryFilterOperation.Equal,
                        Value = CurrentState.RegisterVm.EmailAddress
                    }
                ]);
                if (await HandleFailure(emailContactExistence, action)) return true;
                if (emailContactExistence.Response.TotalItems > 0)
                {
                    SweetAlertService.FireAsync("Email address already exists",$"", SweetAlertIcon.Error);
                    return true;
                }
            }

            return false;
        }

    }
}