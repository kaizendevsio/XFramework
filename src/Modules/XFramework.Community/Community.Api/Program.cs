using XFramework.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using Community.Api.Services;
using Community.Api.Features.CommunityIdentities;
using Community.Api.Features.Connections;
using FluentValidation;
using Community.Api.Features.CommunityIdentities.Create;
using Community.Api.Features.CommunityIdentities.Update;
using Community.Domain.Shared.Contracts.Requests;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Community.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Community");

// Register Community services
builder.Services.AddScoped<ICommunityService, CommunityService>();

// Register validators
builder.Services.AddScoped<IValidator<CreateCommunityIdentityRequest>, CreateCommunityIdentityValidator>();
builder.Services.AddScoped<IValidator<UpdateCommunityIdentityRequest>, UpdateCommunityIdentityValidator>();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<AppDbContext>();
app.MapXFrameworkHealthChecks("Community");

// Map Community endpoints (VSA Feature-Centric Architecture)
app.MapCommunityIdentityEndpoints();
app.MapConnectionEndpoints();

app.Run();