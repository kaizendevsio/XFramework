using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public abstract class IdentityFavoriteValidatorBase<T> : AbstractValidator<T>
{
    protected IdentityFavoriteValidatorBase(
        Expression<Func<T, Guid?>> favoriteTypeId,
        Expression<Func<T, Guid>> credentialId,
        Expression<Func<T, string?>> data)
    {
        RuleFor(favoriteTypeId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Favorite type ID must be a valid identifier when provided.");
        RuleFor(credentialId).NotEmpty();
        RuleFor(data).MaximumLength(5000);
    }
}

public sealed class IdentityFavoriteValidator : IdentityFavoriteValidatorBase<IdentityFavorite>
{
    public IdentityFavoriteValidator(DbContext? dbContext = null)
        : base(x => x.FavoriteTypeId, x => x.CredentialId, x => x.Data)
    {
        if (dbContext is null)
            return;

        RuleFor(x => x.CredentialId).MustAsync(
            (favorite, id, ct) => TenantRelationshipValidation.ExistsAsync<IdentityCredential>(
                dbContext, id, favorite.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.FavoriteTypeId).MustAsync(
            (favorite, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<RegistryFavoriteType>(
                dbContext, id, favorite.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
    }
}

public sealed class CreateIdentityFavoriteRequestValidator : IdentityFavoriteValidatorBase<CreateIdentityFavoriteRequest>
{
    public CreateIdentityFavoriteRequestValidator()
        : base(x => x.FavoriteTypeId, x => x.CredentialId, x => x.Data)
    {
    }
}

public sealed class UpdateIdentityFavoriteRequestValidator : IdentityFavoriteValidatorBase<UpdateIdentityFavoriteRequest>
{
    public UpdateIdentityFavoriteRequestValidator()
        : base(x => x.FavoriteTypeId, x => x.CredentialId, x => x.Data)
    {
    }
}
