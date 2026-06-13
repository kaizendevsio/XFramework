using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace XFramework.Core.Tests.Drivers;

[TestFixture]
public sealed class RuntimeDriverTests
{
    [Test]
    public void DriverBase_InitializeWithoutDerivedTarget_ThrowsExplicitUnsupportedError()
    {
        var driver = new DriverBase();

        Action act = driver.Initialize;

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage("*does not define a target Bolt client*");
    }

    [Test]
    public async Task DriverBase_SendVoidAsync_UsesDerivedTargetClient()
    {
        var messageBus = new Mock<IMessageBusWrapper>();
        var expected = new CmdResponse { HttpStatusCode = HttpStatusCode.Accepted };
        messageBus
            .Setup(x => x.SendVoidAsync(It.IsAny<TestRequest>(), "target-client"))
            .ReturnsAsync(expected);

        var driver = new TestDriver(messageBus.Object, new ConfigurationBuilder().Build());

        var result = await driver.SendVoidAsync(new TestRequest());

        result.Should().BeSameAs(expected);
        messageBus.Verify(x => x.SendVoidAsync(It.IsAny<TestRequest>(), "target-client"), Times.Once);
    }

    [Test]
    public async Task RecordsDriver_NewLogWithMetadata_ReturnsGeneratedLogId()
    {
        var driver = new RecordsDriver(Mock.Of<IMessageBusWrapper>());

        var result = await driver.NewLog(
            "name",
            "message",
            "initiator",
            new RequestMetadata());

        result.Should().NotBeNull();
        result.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task RecordsDriver_NewAuthorizationLog_ReturnsProvidedCredentialId()
    {
        var driver = new RecordsDriver(Mock.Of<IMessageBusWrapper>());
        var credentialId = Guid.NewGuid();

        var result = await driver.NewAuthorizationLog(AuthenticationState.Authenticated, credentialId);

        result.Should().Be(credentialId);
    }

    private sealed record TestDriver(
        IMessageBusWrapper MessageBusDriver,
        IConfiguration Configuration)
        : DriverBase(MessageBusDriver, Configuration)
    {
        public override void Initialize()
        {
            TargetClient = "target-client";
        }
    }

    private sealed class TestRequest : IHasRequestServer
    {
        public RequestMetadata? Metadata { get; set; }
    }
}
