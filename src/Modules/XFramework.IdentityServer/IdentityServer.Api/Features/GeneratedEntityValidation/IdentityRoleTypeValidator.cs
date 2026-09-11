using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityRoleTypeValidator : AbstractValidator<IdentityRoleType>
{
    public IdentityRoleTypeValidator(DbContext dbContext)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GroupId).NotEmpty().MustAsync(
            (role, id, ct) => TenantRelationshipValidation.ExistsAsync<IdentityRoleTypeGroup>(
                dbContext, id, role.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct))
            .WithMessage("Role group must be active and belong to the same tenant.");
    }
}
