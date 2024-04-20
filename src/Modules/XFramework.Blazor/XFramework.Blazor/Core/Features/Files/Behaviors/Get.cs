using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Files.Behaviors;

public class FilesState
{
    public record Get(Guid Identifier) : StateAction<PaginatedResult<StorageFile>?>;

    protected class GetHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store
    )
        : StateActionHandler<Get, PaginatedResult<StorageFile>?>(handlerServices, store)
    {
        public override async Task<PaginatedResult<StorageFile>?> Handle(Get action, CancellationToken aCancellationToken)
        {
            var filters = new List<QueryFilter>
            {
                new()
                {
                    PropertyName = nameof(StorageFile.Identifier),
                    Operation = QueryFilterOperation.Equal,
                    Value = action.Identifier
                }
            };
            
            var response = await identityServerServiceWrapper.StorageFile.GetList(
                pageSize: 100,
                pageNumber: 1,
                filter: filters,
                includeNavigations: true,
                navigationDepth: 1);

            return response.Response;
        }
    }
}