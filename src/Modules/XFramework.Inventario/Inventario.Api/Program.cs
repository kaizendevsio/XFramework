using Inventario.Api.Features.Products;

var builder = XApplication.Build<Program>();
var webApp = (WebApplication)builder.EnsureDatabase<DbContext>();

// Map manual VSA endpoints
webApp.MapProductEndpoints();

webApp.Run();