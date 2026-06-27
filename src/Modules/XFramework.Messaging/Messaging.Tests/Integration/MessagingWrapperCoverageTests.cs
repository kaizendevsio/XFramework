using Bolt.Domain.Shared.Contracts.Requests;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Integration.Drivers;
using NUnit.Framework;

namespace Messaging.Tests.Integration;

[TestFixture]
public sealed class MessagingWrapperCoverageTests
{
    [Test]
    public void MessagingServiceWrapper_AllBoltRequestsHaveWrapperMethod()
    {
        var requestTypes = typeof(CreateThreadRequest).Assembly
            .GetTypes()
            .Where(static type => type is { IsAbstract: false, IsInterface: false })
            .Where(static type => type.GetInterfaces().Any(IsBoltRequestInterface))
            .Select(static type => type.Name)
            .Where(static name => name.EndsWith("Request", StringComparison.Ordinal))
            .Select(static name => name[..^"Request".Length])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name)
            .ToArray();

        var wrapperMethods = typeof(IMessagingServiceWrapper)
            .GetMethods()
            .Select(static method => method.Name.EndsWith("Async", StringComparison.Ordinal)
                ? method.Name[..^"Async".Length]
                : method.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = requestTypes
            .Where(requestName => !wrapperMethods.Contains(requestName))
            .ToArray();

        Assert.That(
            missing,
            Is.Empty,
            "tenant applications should be able to call every Messaging Bolt request through IMessagingServiceWrapper");
    }

    private static bool IsBoltRequestInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IBoltRequest<,>);
}
