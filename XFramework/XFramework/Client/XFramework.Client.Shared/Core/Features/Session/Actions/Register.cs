﻿using IdentityServer.Domain.Generic;
using IdentityServer.Integration.Drivers;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Session;
public partial class SessionState
{
    public record Register : NavigableRequest, IRequest<CmdResponse>
    {
        public bool AutoLogin { get; set; } = true;
        public bool SkipVerification { get; set; }
        public List<Guid>? WalletList { get; set; }
        public bool IsSilent { get; set; }
        public List<Guid>? RoleList { get; set; }
    }
    
    protected class RegisterStateActionHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Register>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(Register action, CancellationToken aCancellationToken)
        {
            IsSilent = action.IsSilent;
            CurrentState.RegisterVm.RoleList ??= action.RoleList;
            
            // Inform UI About Busy State
            await ReportBusy("Creating Account..", true);

            // Check If Any Given Data Are Already Existing
            if (await CheckDuplicateRecords(action)) return;

            // Check If Passwords Are Correct
            if (!CurrentState.RegisterVm.Password.Equals(CurrentState.RegisterVm.PasswordConfirmation))
            {
                SweetAlertService.FireAsync("Password does not match",$"", SweetAlertIcon.Error);
                return;
            }
            
            // Create Guids
            var identityId = Guid.NewGuid();
            var credentialId = Guid.NewGuid();

            // Send Create Identity Request
            await ReportBusy("Creating identity..", null);
            var identityRequest = CurrentState.RegisterVm.Adapt<IdentityInformation>();
            identityRequest.Id = identityId;
            
            var identity = await identityServerServiceWrapper.IdentityInformation.Create(identityRequest);
            if (await HandleFailure(identity, action)) return;
            
            // Send Create Credential Request
            await ReportBusy("Creating credential..", null);
            var credentialRequest = CurrentState.RegisterVm.Adapt<IdentityCredential>();
            credentialRequest.Id = credentialId;
            credentialRequest.IdentityInfoId = identityId;
            
            var credential = await identityServerServiceWrapper.IdentityCredential.Create(credentialRequest);
            if (await HandleFailure(credential, action)) return;
            
            // Send Create Phone Contact Request
            await ReportBusy("Creating contacts..", null);

            var phoneContactType = await identityServerServiceWrapper.IdentityContactType.Get(IdentityConstants.ContactType.Phone);
            var phoneContact = await identityServerServiceWrapper.IdentityContact.Create(new(){
                CredentialId = credentialId,
                TypeId = phoneContactType.Response.Id,
                GroupId = IdentityConstants.ContactGroup.Personal,
                Value = !string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber) ? CurrentState.RegisterVm.PhoneNumber : string.Empty,
                /*SendOtp = !action.SkipVerification*/
            });
            if (await HandleFailure(phoneContact, action)) return;
            
            // Send Create Email Contact Request
            await ReportBusy("Creating contacts..", null);
            var emailContactType = await identityServerServiceWrapper.IdentityContactType.Get(IdentityConstants.ContactType.Email);
            var emailContact = await identityServerServiceWrapper.IdentityContact.Create(new(){
                CredentialId = credentialId,
                TypeId = emailContactType.Response.Id,
                GroupId = IdentityConstants.ContactGroup.Personal,
                Value = !string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress) ? CurrentState.RegisterVm.EmailAddress : string.Empty,
                /*SendOtp = !action.SkipVerification*/
            });
            if (await HandleFailure(emailContact, action)) return;

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

                SessionState.LoginVm.Username = username; 
                SessionState.LoginVm.Password = CurrentState.RegisterVm.Password; 
                await Mediator.Send(new Login()
                {
                    NavigateToOnSuccess = action.NavigateToOnSuccess,
                    SkipVerification = action.SkipVerification,
                    Role = action.RoleList.First()
                });
            
                return;
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(credential, action);

            // Inform UI About Not Busy State
            ReportBusy("", false);
            
            return;
        }

        private async Task CreateWallets(List<Guid> walletList, Guid credentialGuid)
        {
            foreach (var WalletTypeGuid in walletList)
            {
                await Mediator.Send(new WalletState.CreateWallet
                {
                    CredentialId = credentialGuid,
                    WalletTypeId = WalletTypeGuid,
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
            var credentialExistence = await identityServerServiceWrapper.IdentityCredential.GetList(pageSize:1, pageNumber:1, filter: new()
            {
                new()
                {
                    PropertyName = nameof(IdentityCredential.UserName),
                    Operation = QueryFilterOperation.Equal,
                    Value = CurrentState.RegisterVm.UserName
                }
            });
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
                var phoneContactExistence = await identityServerServiceWrapper.IdentityContact.GetList(pageSize:1, pageNumber:1, filter: new()
                {
                    new()
                    {
                        PropertyName = nameof(IdentityContact.Value),
                        Operation = QueryFilterOperation.Equal,
                        Value = CurrentState.RegisterVm.PhoneNumber
                    }
                });
                
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
                var emailContactExistence = await identityServerServiceWrapper.IdentityContact.GetList(pageSize:1, pageNumber:1, filter: new()
                {
                    new()
                    {
                        PropertyName = nameof(IdentityContact.Value),
                        Operation = QueryFilterOperation.Equal,
                        Value = CurrentState.RegisterVm.EmailAddress
                    }
                });
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