using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace XFramework.Core.DataAccess.Commands;

public class CreateHandler<TModel>(
        AppDbContext appDbContext,
        ILogger<CreateHandler<TModel>> logger
    )
    : ICreateHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{
    public async Task<CmdResponse<TModel>> Handle(Create<TModel> request, CancellationToken cancellationToken)
    {
        if (request.Model is null)
        {
            logger.LogWarning("Attempt to create an entity of type {EntityName} with a null model", typeof(TModel).Name);
            throw new ArgumentException("An error occurred while processing your request");
        }

        var tenant = await GetTenant(request.Model.TenantId);

        // Set the CreatedAt property to the current UTC time.
        request.Model.CreatedAt = DateTime.UtcNow;
        request.Model.TenantId = tenant.Id;

        try
        {
            // Add the entity to the context.
            appDbContext.Set<TModel>().Add(request.Model);

            // Save the changes.
            await appDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Entity of type {EntityName} was successfully created", typeof(TModel).Name);

            // Return a successful response.
            return new CmdResponse<TModel>
            {
                Response = request.Model,
                HttpStatusCode = HttpStatusCode.OK
                // ... Add other necessary properties or feedback.
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while creating entity of type {EntityName}", typeof(TModel).Name);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }

    private async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        var entity = await appDbContext.Tenants
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        ArgumentNullException.ThrowIfNull(entity, "Tenant");

        return entity;
    }
}