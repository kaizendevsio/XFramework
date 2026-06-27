using Communications.Api.Features.Messages.MarkRead;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using NUnit.Framework;

namespace Communications.Tests.Features.Messages.MarkRead;

public sealed class MarkMessagesReadValidatorTests
{
    [Test]
    public void Validate_ValidReadReceiptRequest_ReturnsValidResult()
    {
        var validator = new MarkMessagesReadValidator();
        var request = new MarkMessagesReadRequest
        {
            ThreadId = Guid.NewGuid(),
            RequesterCredentialId = Guid.NewGuid(),
            MessageIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_DuplicateMessageIds_ReturnsInvalidResult()
    {
        var messageId = Guid.NewGuid();
        var validator = new MarkMessagesReadValidator();
        var request = new MarkMessagesReadRequest
        {
            ThreadId = Guid.NewGuid(),
            RequesterCredentialId = Guid.NewGuid(),
            MessageIds = [messageId, messageId]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(MarkMessagesReadRequest.MessageIds)), Is.True);
    }

    [Test]
    public void Validate_EmptyMessageId_ReturnsInvalidResult()
    {
        var validator = new MarkMessagesReadValidator();
        var request = new MarkMessagesReadRequest
        {
            ThreadId = Guid.NewGuid(),
            RequesterCredentialId = Guid.NewGuid(),
            MessageIds = [Guid.Empty]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == $"{nameof(MarkMessagesReadRequest.MessageIds)}[0]"), Is.True);
    }
}
