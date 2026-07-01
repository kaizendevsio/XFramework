using System.Reflection;
using POS.Domain.Shared.Contracts.Requests;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperContractTests
{
    [Test]
    public void PosWrapper_ExposesEveryBoltRequestContract()
    {
        var methods = typeof(IPOSServiceWrapper)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(method => method.Name, StringComparer.Ordinal);

        foreach (var requestType in GetPosBoltRequestTypes())
        {
            var methodName = requestType.Name[..^"Request".Length];
            methods.Should().ContainKey(methodName);
            var method = methods[methodName];

            method.GetParameters().Should().ContainSingle(parameter => parameter.ParameterType == requestType);
            method.ReturnType.Should().Be(typeof(Task<>).MakeGenericType(GetBoltResponseType(requestType)!));
        }
    }

    [Test]
    public void PosWrapper_TypedCommands_ReturnTypedCommandPayloads()
    {
        var typedCommandRequests = GetPosBoltRequestTypes()
            .Select(requestType => new
            {
                RequestType = requestType,
                ResponseType = GetBoltResponseType(requestType)!
            })
            .Where(item => item.ResponseType.IsGenericType &&
                item.ResponseType.GetGenericTypeDefinition() == typeof(CmdResponse<>))
            .ToArray();

        typedCommandRequests.Select(item => item.RequestType).Should().Contain(
            [
                typeof(CreatePosRegisterRequest),
                typeof(UpdatePosRegisterRequest),
                typeof(CreatePosCartRequest),
                typeof(UpdatePosCartRequest),
                typeof(SuspendPosCartRequest),
                typeof(ResumePosCartRequest),
                typeof(CancelPosCartRequest),
                typeof(CheckoutPosCartRequest),
                typeof(CheckoutPosSaleRequest),
                typeof(CancelPosSaleRequest),
                typeof(RetryPosSaleFulfillmentRequest),
                typeof(CreatePosReturnRequest)
            ]);

        foreach (var item in typedCommandRequests)
        {
            var method = typeof(IPOSServiceWrapper).GetMethod(item.RequestType.Name[..^"Request".Length]);
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<>).MakeGenericType(item.ResponseType));
        }
    }

    private static IEnumerable<Type> GetPosBoltRequestTypes() =>
        typeof(SearchPosCatalogRequest).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsGenericType: false } &&
                type.Name.EndsWith("Request", StringComparison.Ordinal) &&
                GetBoltResponseType(type) is not null)
            .OrderBy(type => type.Name);

    private static Type? GetBoltResponseType(Type requestType) =>
        requestType
            .GetInterfaces()
            .FirstOrDefault(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition().FullName == "Bolt.Domain.Shared.Contracts.Requests.IBoltRequest`2")
            ?.GetGenericArguments()[1];
}
