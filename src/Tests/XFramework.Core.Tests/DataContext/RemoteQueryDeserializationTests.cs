using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using MemoryPack;
using NUnit.Framework;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed partial class RemoteQueryDeserializationTests
{
    [TestCase(400)]
    [TestCase(403)]
    [TestCase(409)]
    [TestCase(500)]
    public void Deserialize_CountFailure_ThrowsInsteadOfReturningPayloadPrefix(int statusCode)
    {
        var bytes = MemoryPackSerializer.Serialize(DataContextResult.Failure("Query failed", statusCode));

        var action = () => Deserialize<int>(bytes, QueryExecutionMode.Count);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'Count' failed: Query failed");
    }

    [Test]
    public void Deserialize_FailureAcrossScalarAndMaterializedResults_ReportsFailure()
    {
        var bytes = MemoryPackSerializer.Serialize(DataContextResult.Failure("Access denied", 403));
        Action[] actions =
        [
            () => Deserialize<bool>(bytes, QueryExecutionMode.Any),
            () => Deserialize<bool>(bytes, QueryExecutionMode.AnyWithPredicate),
            () => Deserialize<bool>(bytes, QueryExecutionMode.All),
            () => Deserialize<decimal>(bytes, QueryExecutionMode.Sum),
            () => Deserialize<double>(bytes, QueryExecutionMode.Average),
            () => Deserialize<int?>(bytes, QueryExecutionMode.Min),
            () => Deserialize<int>(bytes, QueryExecutionMode.Max),
            () => Deserialize<QueryEntity>(bytes, QueryExecutionMode.FirstOrDefault),
            () => Deserialize<List<QueryEntity>>(bytes, QueryExecutionMode.ToList)
        ];

        foreach (var action in actions)
            action.Should().Throw<InvalidOperationException>().WithMessage("*failed: Access denied");
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(42)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void Deserialize_SuccessfulCount_PreservesValue(int value)
    {
        Deserialize<int>(MemoryPackSerializer.Serialize(value), QueryExecutionMode.Count).Should().Be(value);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Deserialize_SuccessfulBoolean_PreservesValue(bool value)
    {
        Deserialize<bool>(MemoryPackSerializer.Serialize(value), QueryExecutionMode.Any).Should().Be(value);
    }

    [Test]
    public void Deserialize_SuccessfulAggregates_PreserveScalarValues()
    {
        const decimal sum = -123.45m;
        const double average = 12.5;
        const long minimum = long.MinValue;
        var id = Guid.NewGuid();

        Deserialize<decimal>(MemoryPackSerializer.Serialize(sum), QueryExecutionMode.Sum).Should().Be(sum);
        Deserialize<double>(MemoryPackSerializer.Serialize(average), QueryExecutionMode.Average).Should().Be(average);
        Deserialize<long>(MemoryPackSerializer.Serialize(minimum), QueryExecutionMode.Min).Should().Be(minimum);
        Deserialize<Guid>(MemoryPackSerializer.Serialize(id), QueryExecutionMode.Max).Should().Be(id);
        Deserialize<string>(MemoryPackSerializer.Serialize("Query failed"), QueryExecutionMode.Min).Should().Be("Query failed");
    }

    [TestCase(null)]
    [TestCase(4)]
    public void Deserialize_SuccessfulNullableScalar_PreservesValue(int? value)
    {
        Deserialize<int?>(MemoryPackSerializer.Serialize(value), QueryExecutionMode.Min).Should().Be(value);
    }

    [Test]
    public void Deserialize_SuccessfulEntityWithFailurePrefix_PreservesEntireEntity()
    {
        // The first four Guid bytes look like an empty PersistedEntities list, leaving twelve bytes unread.
        var entity = new QueryEntity
        {
            Flag = false, Label = "A real entity", Code = 403,
            Id = Guid.Parse("00000000-1234-5678-90ab-cdef12345678")
        };
        var bytes = MemoryPackSerializer.Serialize(entity);
        DataContextResult? prefix = null;
        var consumed = MemoryPackSerializer.Deserialize(bytes, ref prefix);
        consumed.Should().BeLessThan(bytes.Length);
        prefix!.IsSuccess.Should().BeFalse();
        prefix.Message.Should().Be(entity.Label);

        Deserialize<QueryEntity>(bytes, QueryExecutionMode.FirstOrDefault).Should().BeEquivalentTo(entity);
    }

    [Test]
    public void Deserialize_SuccessfulEntityWithExactFailureLayout_PrefersExpectedResultType()
    {
        var entity = new FailureShapedEntity { Label = "Stored data", Code = 403 };
        var bytes = MemoryPackSerializer.Serialize(entity);
        MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!.IsSuccess.Should().BeFalse();

        Deserialize<FailureShapedEntity>(bytes, QueryExecutionMode.FirstOrDefault).Should().BeEquivalentTo(entity);
    }

    [Test]
    public void Deserialize_SuccessfulListAndNullResponses_PreserveResults()
    {
        var entities = new List<QueryEntity>
        {
            new() { Id = Guid.NewGuid(), Flag = true, Label = "First", Code = 7 },
            new() { Id = Guid.NewGuid(), Flag = false, Label = "Second", Code = 9 }
        };

        Deserialize<List<QueryEntity>>(MemoryPackSerializer.Serialize(entities), QueryExecutionMode.ToList)
            .Should().BeEquivalentTo(entities);
        Deserialize<List<QueryEntity>>(MemoryPackSerializer.Serialize(new List<QueryEntity>()), QueryExecutionMode.ToList)
            .Should().BeEmpty();
        Deserialize<QueryEntity?>(MemoryPackSerializer.Serialize<QueryEntity?>(null), QueryExecutionMode.FirstOrDefault)
            .Should().BeNull();
        Deserialize<List<QueryEntity>?>(MemoryPackSerializer.Serialize<List<QueryEntity>?>(null), QueryExecutionMode.ToList)
            .Should().BeNull();
    }

    [Test]
    public void Deserialize_FailurePrefixWithTrailingBytes_IsInvalidRatherThanRecognizedFailure()
    {
        var bytes = MemoryPackSerializer.Serialize(DataContextResult.Failure("Prefix only", 403)).Append((byte)42).ToArray();

        var action = () => Deserialize<int>(bytes, QueryExecutionMode.Count);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'Count' returned an invalid response:*");
    }

    [Test]
    public void Deserialize_EmptyOrMalformedResponse_StillReportsInvalidResponse()
    {
        var empty = () => Deserialize<int>([], QueryExecutionMode.Count);
        var malformed = () => Deserialize<int>([1, 2], QueryExecutionMode.Count);

        empty.Should().Throw<InvalidOperationException>().WithMessage("*returned an empty response.*");
        malformed.Should().Throw<InvalidOperationException>().WithMessage("*returned an invalid response:*");
    }

    private static TResult Deserialize<TResult>(byte[] bytes, QueryExecutionMode mode) =>
        typeof(RemoteQuery<QueryEntity>)
            .GetMethod("DeserializeOptionalQueryResult", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TResult))
            .CreateDelegate<Func<byte[], QueryExecutionMode, TResult>>()(bytes, mode);

    [MemoryPackable]
    public sealed partial class QueryEntity
    {
        public bool Flag { get; set; }
        public string? Label { get; set; }
        public int Code { get; set; }
        public Guid Id { get; set; }
    }

    [MemoryPackable]
    public sealed partial class FailureShapedEntity
    {
        public bool Flag { get; set; }
        public string? Label { get; set; }
        public int Code { get; set; }
        public List<PersistedEntityState> Values { get; set; } = [];
    }
}
