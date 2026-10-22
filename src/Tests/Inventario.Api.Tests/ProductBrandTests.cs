using FluentAssertions;
using NUnit.Framework;
using Inventario.Api.Features.Brands.Merge;
using Inventario.Api.Features.Brands.PreviewMerge;
using Inventario.Api.Features.Brands.Search;
using Inventario.Api.Features.Products.Create;
using Inventario.Api.Features.Products.Update;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Inventario.Domain.Shared.Contracts.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class ProductBrandTests
{
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase(" \t\r\n", null)]
    [TestCase(" Acme ", "ACME")]
    [TestCase("aCMe   Tools", "ACME TOOLS")]
    [TestCase("Acme\t \r\nTools", "ACME TOOLS")]
    [TestCase("Acme\u00a0\u2003Tools", "ACME TOOLS")]
    public void Normalize_CaseAndWhitespace_ProducesCanonicalValue(string? value, string? expected)
    {
        ProductBrand.Normalize(value).Should().Be(expected);
        ProductBrand.Normalize(ProductBrand.Normalize(value)).Should().Be(expected);
    }

    [Test]
    public void Normalize_DifferentBrands_RemainDifferent() =>
        ProductBrand.Normalize("AC-ME").Should().NotBe(ProductBrand.Normalize("AC ME"));

    [TestCase(typeof(SearchProductBrandsEndpoint), false)]
    [TestCase(typeof(PreviewProductBrandMergeEndpoint), true)]
    [TestCase(typeof(MergeProductBrandsEndpoint), true)]
    public void Endpoints_EnforceMatchingTenantAndManagementPoliciesOnBothTransports(Type endpoint, bool manage)
    {
        var method = endpoint.GetMethod("Handle")!;
        var bolt = method.GetCustomAttributes(typeof(BoltHandlerAttribute), false).Cast<BoltHandlerAttribute>().Single();
        var rest = method.GetCustomAttributes(typeof(MapEndpointAttribute), false).Cast<MapEndpointAttribute>().Single();
        bolt.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        rest.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        bolt.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
        rest.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
        bolt.AllowAnonymous.Should().BeFalse();
        rest.AllowAnonymous.Should().BeFalse();
        bolt.ActorRequirement.Should().Be(ActorRequirement.Required);
        rest.ActorRequirement.Should().Be(ActorRequirement.Required);
        rest.RequireAuthorization.Should().BeTrue();
        if (manage)
        {
            bolt.RequiredActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
            rest.RequiredActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
        }
    }

    [Test]
    public void Validators_RejectMissingCategoryEmptyBrandsAndUnconfirmedMerge()
    {
        new SearchProductBrandsValidator().Validate(new SearchProductBrandsRequest()).IsValid.Should().BeFalse();
        new PreviewProductBrandMergeValidator().Validate(new PreviewProductBrandMergeRequest() { CategoryId = Guid.NewGuid(), SourceBrand = " ", TargetBrand = "A" })
            .IsValid.Should().BeFalse();
        var validator = new MergeProductBrandsValidator();
        var request = new XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands.MergeProductBrandsRequest
        {
            CategoryId = Guid.NewGuid(), SourceBrand = "A", TargetBrand = "B", ExpectedProductCount = 1
        };
        validator.Validate(request).IsValid.Should().BeFalse();
        validator.Validate(request with { Confirmed = true }).IsValid.Should().BeTrue();
        validator.Validate(request with { Confirmed = true, ExpectedProductCount = 0 }).IsValid.Should().BeFalse();
        validator.Validate(request with { Confirmed = true, TargetBrand = new string('x', 101) }).IsValid.Should().BeFalse();
    }

    [Test]
    public void ProductValidators_UseNormalizedBrandLength()
    {
        var brand = "  " + new string('a', 100) + "  ";
        new CreateProductValidator().Validate(new CreateProductRequest() { Name = "Product", Price = 1, CategoryId = Guid.NewGuid(), Brand = brand })
            .IsValid.Should().BeTrue();
        new UpdateProductValidator().Validate(new UpdateProductRequest() { Name = "Product", Price = 1, CategoryId = Guid.NewGuid(), Brand = brand })
            .IsValid.Should().BeTrue();
    }
}

