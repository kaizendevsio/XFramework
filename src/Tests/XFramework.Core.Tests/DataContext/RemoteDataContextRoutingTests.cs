using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Integration.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public class RemoteDataContextRoutingTests
{
    [Test]
    public void GetServiceWrapperMap_MergesGeneratedRegistrationMapsFromLoadedIntegrationAssemblies()
    {
        Assembly.Load("IdentityServer.Integration");
        Assembly.Load("Wallets.Integration");
        ClearWrapperMapCache();

        var wrapperMap = GetServiceWrapperMap();

        wrapperMap["Tenant"].Should().Be("IdentityServer.Integration.Drivers.IIdentityServerServiceWrapper");
        wrapperMap["StorageFile"].Should().Be("IdentityServer.Integration.Drivers.IIdentityServerServiceWrapper");
        wrapperMap["Wallet"].Should().Be("Wallets.Integration.Drivers.IWalletsServiceWrapper");
        wrapperMap["WalletTransaction"].Should().Be("Wallets.Integration.Drivers.IWalletsServiceWrapper");
    }

    private static Dictionary<string, string> GetServiceWrapperMap()
    {
        var method = typeof(RemoteDataContext).GetMethod(
            "GetServiceWrapperMap",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        var map = (Dictionary<string, string>?)method!.Invoke(null, null);
        map.Should().NotBeNull();
        return map!;
    }

    private static void ClearWrapperMapCache()
    {
        var field = typeof(RemoteDataContext).GetField(
            "_wrapperMap",
            BindingFlags.NonPublic | BindingFlags.Static);

        field.Should().NotBeNull();
        field!.SetValue(null, null);
    }
}
