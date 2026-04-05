using FluentAssertions;
using MemoryPack;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public class QueryDescriptorSerializationTests
{
    [Test]
    public void QueryDescriptor_RoundTrip_PreservesAllFields()
    {
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "Product",
            Filters =
            [
                new QueryFilter
                {
                    PropertyName = "Price",
                    Operation = QueryFilterOperation.GreaterThan,
                    Value = 100
                }
            ],
            Sorting =
            [
                new SortDescriptor { PropertyName = "Name", Descending = false, IsSecondary = false }
            ],
            Skip = 10,
            Take = 20,
            Includes = ["Category"],
            Mode = QueryExecutionMode.ToList,
            NoCache = true,
            ApplyDistinct = false,
            DistinctByProperty = "Status",
            AggregateProperty = "Price",
            GroupByProperty = "Category"
        };

        var bytes = MemoryPackSerializer.Serialize(descriptor);
        var deserialized = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes);

        deserialized.Should().NotBeNull();
        deserialized!.EntityTypeName.Should().Be("Product");
        deserialized.Filters.Should().HaveCount(1);
        deserialized.Filters[0].PropertyName.Should().Be("Price");
        deserialized.Filters[0].Operation.Should().Be(QueryFilterOperation.GreaterThan);
        deserialized.Sorting.Should().HaveCount(1);
        deserialized.Sorting[0].PropertyName.Should().Be("Name");
        deserialized.Skip.Should().Be(10);
        deserialized.Take.Should().Be(20);
        deserialized.Includes.Should().Contain("Category");
        deserialized.Mode.Should().Be(QueryExecutionMode.ToList);
        deserialized.NoCache.Should().BeTrue();
        deserialized.DistinctByProperty.Should().Be("Status");
        deserialized.AggregateProperty.Should().Be("Price");
        deserialized.GroupByProperty.Should().Be("Category");
    }

    [Test]
    public void SortDescriptor_RoundTrip_PreservesAllFields()
    {
        var sort = new SortDescriptor
        {
            PropertyName = "Price",
            Descending = true,
            IsSecondary = true
        };

        var bytes = MemoryPackSerializer.Serialize(sort);
        var deserialized = MemoryPackSerializer.Deserialize<SortDescriptor>(bytes);

        deserialized.Should().NotBeNull();
        deserialized!.PropertyName.Should().Be("Price");
        deserialized.Descending.Should().BeTrue();
        deserialized.IsSecondary.Should().BeTrue();
    }

    [Test]
    public void ChangeEntry_RoundTrip_PreservesAllFields()
    {
        var entry = new ChangeEntry
        {
            EntityTypeName = "Product",
            Operation = ChangeOperation.Add,
            SerializedEntity = [1, 2, 3, 4]
        };

        var bytes = MemoryPackSerializer.Serialize(entry);
        var deserialized = MemoryPackSerializer.Deserialize<ChangeEntry>(bytes);

        deserialized.Should().NotBeNull();
        deserialized!.EntityTypeName.Should().Be("Product");
        deserialized.Operation.Should().Be(ChangeOperation.Add);
        deserialized.SerializedEntity.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }

    [Test]
    public void SaveChangesRequest_RoundTrip_PreservesChanges()
    {
        var request = new SaveChangesRequest
        {
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = "Product",
                    Operation = ChangeOperation.Update,
                    SerializedEntity = [10, 20]
                }
            ]
        };

        var bytes = MemoryPackSerializer.Serialize(request);
        var deserialized = MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes);

        deserialized.Should().NotBeNull();
        deserialized!.Changes.Should().HaveCount(1);
        deserialized.Changes[0].EntityTypeName.Should().Be("Product");
    }

    [Test]
    public void DataContextResult_RoundTrip_PreservesAllFields()
    {
        var result = DataContextResult.Failure("Something went wrong", 500);

        var bytes = MemoryPackSerializer.Serialize(result);
        var deserialized = MemoryPackSerializer.Deserialize<DataContextResult>(bytes);

        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeFalse();
        deserialized.Message.Should().Be("Something went wrong");
        deserialized.StatusCode.Should().Be(500);
    }
}
