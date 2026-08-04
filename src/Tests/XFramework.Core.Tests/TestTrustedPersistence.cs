using System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Security;

namespace XFramework.Core.Tests;

internal static class TestTrustedPersistence
{
    public static AppDbContext Create(DbContextOptions<AppDbContext> options) => new(
        options,
        new HttpContextAccessor(),
        new ConfigurationBuilder().Build(),
        new TrustedTenantAccessor(),
        new CrossTenantSeedAuthorization());

    private sealed class TrustedTenantAccessor : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => null;
    }

    private sealed class CrossTenantSeedAuthorization : ICrossTenantWriteAuthorizationAccessor
    {
        public bool IsAuthorized => true;
    }
}
