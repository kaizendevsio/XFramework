using Messaging.Api.Features.Messages.CreateVerification;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using NUnit.Framework;
using XFramework.Domain.Shared.Enums;

namespace Messaging.Tests.Features.Messages.CreateVerification;

public sealed class CreateVerificationMessageValidatorTests
{
    [Test]
    public void Validate_ValidPhoneVerificationRequest_ReturnsValidResult()
    {
        var validator = new CreateVerificationMessageValidator();
        var request = new CreateVerificationMessageRequest
        {
            VerificationToken = "123456",
            ContactType = GenericContactType.Phone,
            Contact = "+15555550100"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_MissingToken_ReturnsInvalidResult()
    {
        var validator = new CreateVerificationMessageValidator();
        var request = new CreateVerificationMessageRequest
        {
            ContactType = GenericContactType.Phone,
            Contact = "+15555550100"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(CreateVerificationMessageRequest.VerificationToken)), Is.True);
    }

    [Test]
    public void Validate_MissingContactType_ReturnsInvalidResult()
    {
        var validator = new CreateVerificationMessageValidator();
        var request = new CreateVerificationMessageRequest
        {
            VerificationToken = "123456",
            ContactType = GenericContactType.NotSpecified,
            Contact = "+15555550100"
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(CreateVerificationMessageRequest.ContactType)), Is.True);
    }
}
