using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using XFramework.Domain.Migrations;

namespace AttendanceMigrationTests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:Attendance")]
public sealed class AttendanceMigrationOwnershipTests
{
    [Test]
    public void AttendanceBaseline_UpAndDown_OnlyOperateOnAttendanceSchema()
    {
        var migration = new AddAttendanceBaseline();
        var up = BuildOperations(migration, "Up");
        var down = BuildOperations(migration, "Down");

        up.Concat(down)
            .OfType<SqlOperation>()
            .Should()
            .NotContain(operation =>
                operation.Sql.Contains("\"Identity\".", StringComparison.Ordinal) ||
                operation.Sql.Contains("\"Application\".", StringComparison.Ordinal));
    }

    private static IReadOnlyList<MigrationOperation> BuildOperations(Migration migration, string methodName)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var method = migration.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Migration method {methodName} was not found.");

        method.Invoke(migration, [builder]);
        return builder.Operations;
    }
}
