using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public abstract class RegistryConfigurationValidatorBase<T> : AbstractValidator<T>
{
    protected RegistryConfigurationValidatorBase(
        Expression<Func<T, string>> key,
        Expression<Func<T, string?>> value,
        Expression<Func<T, Guid>> groupId,
        Expression<Func<T, string?>> unit)
    {
        RuleFor(key).NotEmpty().MaximumLength(200);
        RuleFor(value).MaximumLength(5000);
        RuleFor(groupId).NotEmpty();
        RuleFor(unit).MaximumLength(100);
    }
}

public sealed class RegistryConfigurationValidator : RegistryConfigurationValidatorBase<RegistryConfiguration>
{
    public RegistryConfigurationValidator(DbContext? dbContext = null)
        : base(x => x.Key, x => x.Value, x => x.GroupId, x => x.Unit)
    {
        if (dbContext is null)
            return;

        RuleFor(x => x.GroupId).MustAsync(
            (configuration, id, ct) => TenantRelationshipValidation.ExistsAsync<RegistryConfigurationGroup>(
                dbContext, id, configuration.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
    }
}

public sealed class CreateRegistryConfigurationRequestValidator : RegistryConfigurationValidatorBase<CreateRegistryConfigurationRequest>
{
    public CreateRegistryConfigurationRequestValidator()
        : base(x => x.Key, x => x.Value, x => x.GroupId, x => x.Unit)
    {
    }
}

public sealed class UpdateRegistryConfigurationRequestValidator : RegistryConfigurationValidatorBase<UpdateRegistryConfigurationRequest>
{
    public UpdateRegistryConfigurationRequestValidator()
        : base(x => x.Key, x => x.Value, x => x.GroupId, x => x.Unit)
    {
    }
}
