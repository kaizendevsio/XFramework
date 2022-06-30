using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateVerificationMessageHandler : CommandBaseHandler, IRequestHandler<CreateVerificationMessageCmd, CmdResponse>
{
    private readonly IMediator _mediator;

    public CreateVerificationMessageHandler(IDataLayer dataLayer, IMediator mediator)
    {
        _mediator = mediator;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateVerificationMessageCmd request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        
        var messageTemplate = await _dataLayer.RegistryConfigurations
            .Where(i => i.ApplicationId == application.Id)
            .Where(i => i.Group.Name == "MessagingService_Otp")
            .FirstOrDefaultAsync(CancellationToken.None);

        if (messageTemplate is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Conflict,
                IsSuccess = true,
                Message = "Unable to send message: OTP message template could not be found"
            };
        }
        
        var otp = request.VerificationToken;
        var message = messageTemplate.Value.Replace("|Value|", $"{otp}");
        var contact = request.Contact;

        switch (request.ContactType)
        {
            case GenericContactType.Phone:
                var t= await _mediator.Send(new CreateDirectMessageCmd()
                {
                    MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
                    Sender = "+630000000000",
                    Recipient = contact,
                    Subject = "One Time Password",
                    Intent = "OTP",
                    Message = message,
                    IsScheduled = false
                });

                if (t.HttpStatusCode is not HttpStatusCode.Accepted) return t;
                break;
        }
        
        if (string.IsNullOrEmpty(contact))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadGateway,
                IsSuccess = false,
                Message = "Unable to send message: Contact number is empty"
            };
        }
        
        return new(){
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Message sent successfully",
            IsSuccess = true
        };
    }
}