﻿using IdentityServer.Domain.Shared;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Blazor.Entity.Models.Requests.Session;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record InitiateVerificationCode : NavigableRequest
    {
        public Guid CredentialId { get; set; }
        public GenericContactType ContactType { get; set; }
        public string Contact { get; set; }
        public bool? LocalVerification { get; set; }
        public string LocalToken { get; set; }
        public Action? OnValidToken { get; set; }
        public Action? OnInvalidToken { get; set; }
    }
    
    protected class InitiateVerificationCodeHandler
    (
        IMessagingServiceWrapper messagingServiceWrapper,
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<InitiateVerificationCode>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(InitiateVerificationCode action, CancellationToken aCancellationToken)
        {
            if (action.LocalVerification is true)
            {
                await messagingServiceWrapper.CreateVerificationMessage(new()
                {
                    VerificationToken = action.LocalToken,
                    ContactType = action.ContactType,
                    Contact = action.Contact
                });
                
                SessionState.VerificationVm = action.Adapt<VerificationRequest>();
            }
            else
            {
                var identityVerificationType = await identityServerServiceWrapper.IdentityVerificationType.GetList(
                    pageNumber: 1,
                    pageSize: 1,
                    filter:
                    [
                        new()
                        {
                            PropertyName = nameof(IdentityVerificationType.SystemReferenceId),
                            Operation = QueryFilterOperation.Equal,
                            Value = IdentityConstants.VerificationType.Sms
                        }
                    ]);

                if (await HandleFailure(identityVerificationType, action))
                {
                    return;
                }

                if (identityVerificationType.Response?.TotalItems == 0)
                {
                    await SweetAlertService.FireAsync(new()
                    {
                        Title = "Error",
                        Text = "Verification type not supported",
                        Icon = SweetAlertIcon.Error,
                        ShowCloseButton = true,
                        ConfirmButtonText = "Close",
                    });
                    return;
                }
                
                var result = await identityServerServiceWrapper.IdentityVerification.Create(new()
                {
                    CredentialId = action.CredentialId,
                    VerificationTypeId = identityVerificationType.Response?.Items.First().Id,
                });

                if (result.IsSuccess is false)
                {
                    await SweetAlertService.FireAsync(new()
                    {
                        Title = "Error",
                        Text = result.Message,
                        Icon = SweetAlertIcon.Error,
                        ShowCloseButton = true,
                        ConfirmButtonText = "Close",
                    });
                    return;
                }

                await Mediator.Send(new SetState()
                {
                    VerificationVm = new()
                    {
                        Id = result.Response?.Id ?? Guid.Empty,
                        CredentialId = action.CredentialId,
                        OnValidToken = action.OnValidToken,
                        OnInvalidToken = action.OnInvalidToken
                    }
                });
                Console.WriteLine("Verification code sent");
            }

            await HandleSuccess(action, "Verification code sent");
        }
    }
}