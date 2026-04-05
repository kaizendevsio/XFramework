using System;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.DataContext.ExpressionVisitor;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public class QueryExpressionVisitorTests
{
    // Test entity
    private class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public ProductStatus Status { get; set; }
        public Category? Category { get; set; }
    }

    private class Category
    {
        public string Name { get; set; } = "";
    }

    private enum ProductStatus { Active, Pending, Archived }

    [Test]
    public void Parse_EqualComparison_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name == "Widget");

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("Name");
        filters[0].Operation.Should().Be(QueryFilterOperation.Equal);
        filters[0].Value.Should().Be("Widget");
    }

    [Test]
    public void Parse_NotEqualComparison_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Price != 0);

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.NotEqual);
    }

    [Test]
    public void Parse_GreaterThan_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Price > 100);

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("Price");
        filters[0].Operation.Should().Be(QueryFilterOperation.GreaterThan);
    }

    [Test]
    public void Parse_LessThanOrEqual_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Stock <= 50);

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.LessThanOrEqual);
    }

    [Test]
    public void Parse_StringContains_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name.Contains("foo"));

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.Contains);
        filters[0].Value.Should().Be("foo");
    }

    [Test]
    public void Parse_StringStartsWith_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name.StartsWith("pre"));

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.StartsWith);
    }

    [Test]
    public void Parse_StringEndsWith_ProducesCorrectFilter()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name.EndsWith("suf"));

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.EndsWith);
    }

    [Test]
    public void Parse_AndCombination_ProducesMultipleFilters()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Price > 10 && x.Stock > 0);

        filters.Should().HaveCount(2);
        filters[0].PropertyName.Should().Be("Price");
        filters[1].PropertyName.Should().Be("Stock");
    }

    [Test]
    public void Parse_BooleanProperty_ProducesEqualTrue()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.IsActive);

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("IsActive");
        filters[0].Operation.Should().Be(QueryFilterOperation.Equal);
        filters[0].Value.Should().Be(true);
    }

    [Test]
    public void Parse_NegatedBoolean_ProducesEqualFalse()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => !x.IsActive);

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.Equal);
        filters[0].Value.Should().Be(false);
    }

    [Test]
    public void Parse_IsNull_ProducesEqualNull()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name == null);

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.Equal);
        filters[0].Value.Should().BeNull();
    }

    [Test]
    public void Parse_IsNotNull_ProducesNotEqualNull()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Name != null);

        filters.Should().HaveCount(1);
        filters[0].Operation.Should().Be(QueryFilterOperation.NotEqual);
        filters[0].Value.Should().BeNull();
    }

    [Test]
    public void Parse_ClosureCapture_EvaluatesVariable()
    {
        var minPrice = 99.99m;
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Price > minPrice);

        filters.Should().HaveCount(1);
        filters[0].Value.Should().Be(minPrice);
    }

    [Test]
    public void Parse_NestedPropertyAccess_ProducesDottedName()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Category!.Name == "Electronics");

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("Category.Name");
    }

    [Test]
    public void Parse_EnumComparison_Works()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Status == ProductStatus.Active);

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("Status");
        filters[0].Operation.Should().Be(QueryFilterOperation.Equal);
    }

    [Test]
    public void Parse_OrEqualsSameProperty_OptimizesToIn()
    {
        var filters = QueryExpressionVisitor.Parse<Product>(
            x => x.Status == ProductStatus.Active || x.Status == ProductStatus.Pending || x.Status == ProductStatus.Archived);

        // Should be optimized to In filters
        filters.Should().AllSatisfy(f => f.Operation.Should().Be(QueryFilterOperation.In));
        filters.Should().AllSatisfy(f => f.PropertyName.Should().Be("Status"));
    }

    [Test]
    public void Parse_RangePattern_ProducesTwoFilters()
    {
        // x.Price is >= 10 and <= 50 compiles to x.Price >= 10 && x.Price <= 50
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Price >= 10 && x.Price <= 50);

        filters.Should().HaveCount(2);
        filters[0].Operation.Should().Be(QueryFilterOperation.GreaterThanOrEqual);
        filters[1].Operation.Should().Be(QueryFilterOperation.LessThanOrEqual);
    }

    [Test]
    public void Parse_GuidComparison_Works()
    {
        var targetId = Guid.NewGuid();
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.Id == targetId);

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("Id");
        filters[0].Value.Should().Be(targetId);
    }

    [Test]
    public void Parse_DateTimeComparison_Works()
    {
        var cutoff = new DateTime(2024, 1, 1);
        var filters = QueryExpressionVisitor.Parse<Product>(x => x.CreatedAt > cutoff);

        filters.Should().HaveCount(1);
        filters[0].PropertyName.Should().Be("CreatedAt");
        filters[0].Operation.Should().Be(QueryFilterOperation.GreaterThan);
    }
}
