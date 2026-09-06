using FluentAssertions;
using NUnit.Framework;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class InventoryLotServiceTests
{
    [Test]
    public void NormalizeUtc_PreservesUtcValue()
    {
        var value = new DateTime(2026, 9, 6, 10, 30, 0, DateTimeKind.Utc);

        InventoryLotService.NormalizeUtc(value).Should().Be(value);
    }

    [Test]
    public void NormalizeUtc_ConvertsLocalValueToUtc()
    {
        var value = new DateTime(2026, 9, 6, 10, 30, 0, DateTimeKind.Local);

        InventoryLotService.NormalizeUtc(value).Should().Be(value.ToUniversalTime());
    }

    [Test]
    public void NormalizeUtc_TreatsUnspecifiedValueAsUtc()
    {
        var value = new DateTime(2026, 9, 6, 10, 30, 0, DateTimeKind.Unspecified);

        var result = InventoryLotService.NormalizeUtc(value);

        result.Should().Be(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void NormalizeUtc_PreservesNull()
    {
        InventoryLotService.NormalizeUtc(null).Should().BeNull();
    }
}
