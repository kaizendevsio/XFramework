using FluentValidation;
using XFramework.Core.DataContext;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

internal sealed class FluentRemoteDataContextEntityValidator<T>(IValidator<T> validator)
    : IRemoteDataContextEntityValidator where T : class
{
    public Type EntityType => typeof(T);

    public async Task<IReadOnlyList<string>> ValidateAsync(object entity, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync((T)entity, ct);
        return result.Errors.Select(error => error.ErrorMessage).Distinct().ToArray();
    }
}
