using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using FluentAssertions;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed partial class DataContextHandlerRegistrationTests
{
    [Test]
    public async Task AddDataContextHandler_LegacyRegistryWithoutAuthorizationPolicies_FailsClosed()
    {
        var registrationAssembly = CreateLegacyRegistrationAssembly();
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<RegistrationTestDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<RegistrationTestDbContext>());
        services.AddDataContextHandler(registrationAssembly);
        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IQueryExecutionService>();
        var request = new SaveChangesRequest
        {
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(LegacyRegisteredEntity),
                    Operation = ChangeOperation.Add,
                    SerializedEntity = MemoryPackSerializer.Serialize(new LegacyRegisteredEntity
                    {
                        Id = Guid.NewGuid()
                    })
                }
            ]
        };

        var responseBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var response = MemoryPackSerializer.Deserialize<DataContextResult>(responseBytes);

        response.Should().NotBeNull();
        response!.IsSuccess.Should().BeFalse();
        response.Message.Should().Contain("not registered for remote mutation");
        response.StatusCode.Should().Be(403);
    }

    private static Assembly CreateLegacyRegistrationAssembly()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"LegacyDataContextRegistry_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var typeBuilder = module.DefineType(
            "Legacy.DataContextEntityRegistrations",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var registrationsField = typeBuilder.DefineField(
            "Registrations",
            typeof(Dictionary<string, Type>),
            FieldAttributes.Public | FieldAttributes.Static);
        var getEntitiesMethod = typeBuilder.DefineMethod(
            "GetDataContextEntityTypes",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(Dictionary<string, Type>),
            Type.EmptyTypes);
        var il = getEntitiesMethod.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, registrationsField);
        il.Emit(OpCodes.Ret);

        var registrationType = typeBuilder.CreateType()!;
        registrationType.GetField("Registrations")!.SetValue(null, new Dictionary<string, Type>
        {
            [nameof(LegacyRegisteredEntity)] = typeof(LegacyRegisteredEntity)
        });
        return assembly;
    }

    [MemoryPackable]
    public partial class LegacyRegisteredEntity
    {
        public Guid Id { get; set; }
    }

    private sealed class RegistrationTestDbContext(DbContextOptions<RegistrationTestDbContext> options)
        : DbContext(options);
}
