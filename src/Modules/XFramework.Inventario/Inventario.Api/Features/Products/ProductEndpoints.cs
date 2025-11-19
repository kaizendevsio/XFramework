using Inventario.Api.Features.Products.Create;
using Inventario.Api.Features.Products.Delete;
using Inventario.Api.Features.Products.Get;
using Inventario.Api.Features.Products.GetList;
using Inventario.Api.Features.Products.Update;

namespace Inventario.Api.Features.Products;

/// <summary>
/// Extension methods for registering Product endpoints
/// </summary>
public static class ProductEndpoints
{
    /// <summary>
    /// Maps all Product endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .WithOpenApi();

        // Map individual endpoints
        app.MapCreateProduct();
        app.MapGetProduct();
        app.MapGetProductsList();
        app.MapUpdateProduct();
        app.MapDeleteProduct();

        return app;
    }
}