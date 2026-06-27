using Communications.Api.Features.Threads.Get;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using NUnit.Framework;

namespace Communications.Tests.Features.Threads.Get;

public sealed class GetThreadValidatorTests
{
    [Test]
    public void Validate_ValidGetThreadRequest_ReturnsValidResult()
    {
        var validator = new GetThreadValidator();
        var request = new GetThreadRequest
        {
            Id = Guid.NewGuid(),
            RequesterCredentialId = Guid.NewGuid()
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_MissingRequesterCredentialId_ReturnsValidResult()
    {
        var validator = new GetThreadValidator();
        var request = new GetThreadRequest
        {
            Id = Guid.NewGuid(),
            RequesterCredentialId = Guid.Empty
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }
}
