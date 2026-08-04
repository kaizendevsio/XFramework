using Attendance.Api.Services;
using Attendance.Integration.Drivers;
using Attendance.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace Attendance.IntegrationTests;

public abstract class AttendanceIntegrationTestBase
{
    protected HttpClient HttpClient { get; private set; } = null!;

    protected static IAttendanceServiceWrapper ServiceWrapper =>
        AttendanceIntegrationTestFixture.ServiceWrapper;

    protected static IDataContext DataContext =>
        AttendanceIntegrationTestFixture.DataContext;

    [SetUp]
    public void BaseSetUp()
    {
        HttpClient = new HttpClient { BaseAddress = new Uri(AttendanceIntegrationTestFixture.AttendanceUrl) };
    }

    [TearDown]
    public void BaseTearDown() => HttpClient?.Dispose();

    protected static AppDbContext CreateDbContext() =>
        AttendanceIntegrationTestFixture.CreateDbContext();

    protected static AttendanceService CreateService(AppDbContext db) =>
        new(
            db,
            NullLogger<AttendanceService>.Instance,
            new AttendanceTestInvocationContextAccessor(AttendanceIntegrationTestFixture.TestTenantId));

    protected static RequestMetadata CreateMetadata(Guid? tenantId = null) => new()
    {
        RequestedTenantId = tenantId ?? AttendanceIntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "AttendanceIntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };

    protected static string UniqueCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(32, prefix.Length + 33)];
}
