using FluentValidation;
using XFramework.Core.DataContext;

namespace Wallets.Api.Features.ReferenceData;

public static class ReferenceDataValidationExtensions
{
    public static IServiceCollection AddWalletsReferenceDataValidation(this IServiceCollection services)
    {
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new ReferenceDataValidator<CurrencyType>(provider.GetRequiredService<IValidator<CurrencyType>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new ReferenceDataValidator<ExchangeRate>(provider.GetRequiredService<IValidator<ExchangeRate>>()));
        return services;
    }

    private sealed class ReferenceDataValidator<T>(IValidator<T> validator) : IRemoteDataContextEntityValidator
        where T : class
    {
        public Type EntityType => typeof(T);

        public async Task<IReadOnlyList<string>> ValidateAsync(object entity, CancellationToken ct = default)
        {
            var result = await validator.ValidateAsync((T)entity, ct);
            return result.Errors.Select(error => error.ErrorMessage).Distinct().ToArray();
        }
    }
}
