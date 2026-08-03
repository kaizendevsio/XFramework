using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public abstract class IdentityContactValidatorBase<T> : AbstractValidator<T>
{
    protected IdentityContactValidatorBase(
        Expression<Func<T, Guid?>> typeId,
        Expression<Func<T, string>> value,
        Expression<Func<T, Guid>> credentialId,
        Expression<Func<T, Guid>> groupId)
    {
        RuleFor(typeId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Contact type ID must be a valid identifier when provided.");
        RuleFor(value).NotEmpty().MaximumLength(500);
        RuleFor(credentialId).NotEmpty();
        RuleFor(groupId).NotEmpty();
    }
}

public sealed class IdentityContactValidator : IdentityContactValidatorBase<IdentityContact>
{
    public IdentityContactValidator(DbContext? dbContext = null)
        : base(x => x.TypeId, x => x.Value, x => x.CredentialId, x => x.GroupId)
    {
        if (dbContext is null)
            return;

        RuleFor(x => x.CredentialId).MustAsync(
            (contact, id, ct) => TenantRelationshipValidation.ExistsAsync<IdentityCredential>(
                dbContext, id, contact.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.GroupId).MustAsync(
            (contact, id, ct) => TenantRelationshipValidation.ExistsAsync<IdentityContactGroup>(
                dbContext, id, contact.TenantId, TenantRelationshipValidation.TenantScope.TenantOrGlobal, ct));
        RuleFor(x => x.TypeId).MustAsync(
            (contact, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<IdentityContactType>(
                dbContext, id, contact.TenantId, TenantRelationshipValidation.TenantScope.TenantOrGlobal, ct));
    }
}

public sealed class CreateIdentityContactRequestValidator : IdentityContactValidatorBase<CreateIdentityContactRequest>
{
    public CreateIdentityContactRequestValidator()
        : base(x => x.TypeId, x => x.Value, x => x.CredentialId, x => x.GroupId)
    {
    }
}

public sealed class UpdateIdentityContactRequestValidator : IdentityContactValidatorBase<UpdateIdentityContactRequest>
{
    public UpdateIdentityContactRequestValidator()
        : base(x => x.TypeId, x => x.Value, x => x.CredentialId, x => x.GroupId)
    {
    }
}
