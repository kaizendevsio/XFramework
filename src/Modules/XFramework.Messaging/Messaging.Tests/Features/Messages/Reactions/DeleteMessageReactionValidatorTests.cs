using Messaging.Api.Features.Messages.Reactions.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using NUnit.Framework;

namespace Messaging.Tests.Features.Messages.Reactions;

public sealed class DeleteMessageReactionValidatorTests
{
    [Test]
    public void Validate_ValidDeleteReactionRequest_ReturnsValidResult()
    {
        var validator = new DeleteMessageReactionValidator();
        var request = new DeleteMessageReactionRequest
        {
            ThreadId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            ReactionId = Guid.NewGuid()
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_MissingRouteContext_ReturnsInvalidResult()
    {
        var validator = new DeleteMessageReactionValidator();
        var request = new DeleteMessageReactionRequest
        {
            ReactionId = Guid.NewGuid()
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(DeleteMessageReactionRequest.ThreadId)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(DeleteMessageReactionRequest.MessageId)), Is.True);
    }
}
