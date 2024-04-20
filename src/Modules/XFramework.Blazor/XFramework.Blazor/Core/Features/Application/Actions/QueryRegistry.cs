using IdentityServer.Integration.Drivers;
using Registry.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Application;

public partial class ApplicationState
{
    public record QueryRegistry(string Key) : QueryAction<PaginatedResult<RegistryConfiguration>?>;
    
    protected class QueryRegistryHandler(
        IRegistryServiceWrapper registryServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<QueryRegistry, PaginatedResult<RegistryConfiguration>?>(handlerServices, store)
    {
        public override async Task<PaginatedResult<RegistryConfiguration>?> Handle(QueryRegistry action, CancellationToken aCancellationToken)
        {
            var configurationList = await registryServiceWrapper.RegistryConfiguration.GetList(
                pageSize: action.PageSize,
                pageNumber: action.PageIndex,
                filter: new()
                {
                    new QueryFilter
                    {
                        PropertyName = $"{nameof(RegistryConfiguration.Key)}",
                        Operation = QueryFilterOperation.Equal,
                        Value = action.Key
                    },
                    new QueryFilter
                    {
                        PropertyName = $"{nameof(RegistryConfiguration.IsEnabled)}",
                        Operation = QueryFilterOperation.Equal,
                        Value = true
                    }
                },
                includeNavigations: true,
                includes: new List<string>
                {
                    $"{nameof(RegistryConfiguration.Group)}"
                });
            
            if (await HandleFailure(configurationList, action)) return null;

            await HandleSuccess(action, "Registry configuration retrieved successfully");

            return configurationList.Response;
        }
    }
}