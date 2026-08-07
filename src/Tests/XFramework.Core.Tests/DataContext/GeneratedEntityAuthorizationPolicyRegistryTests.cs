using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Core.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class GeneratedEntityAuthorizationPolicyRegistryTests
{
    [Test]
    public void Registry_FreezesPolicyCollectionsAndDetachesFromMutableInputs()
    {
        var roles = new List<string> { "Admin" };
        var callers = new List<string> { "XFramework.Portal" };
        var registry = new GeneratedEntityAuthorizationPolicyRegistry(
        [
            new GeneratedEntityAuthorizationPolicy
            {
                EntityTypeName = "SecuredEntity",
                Operation = GeneratedEntityOperation.Read,
                RequiredRoles = roles,
                AllowedServiceCallers = callers
            }
        ]);

        roles[0] = "Changed";
        callers.Clear();

        registry.TryGet("SecuredEntity", GeneratedEntityOperation.Read, out var policy)
            .Should().BeTrue();
        policy.RequiredRoles.Should().BeEquivalentTo("Admin");
        policy.AllowedServiceCallers.Should().BeEquivalentTo("XFramework.Portal");
        ((ICollection<string>)policy.RequiredRoles).Invoking(collection => collection.Add("Changed"))
            .Should().Throw<NotSupportedException>();
    }
}
