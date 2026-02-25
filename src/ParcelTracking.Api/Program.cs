using Microsoft.EntityFrameworkCore;
using ParcelTracking.Infrastructure.Data;
using ParcelTracking.Application.Services;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Infrastructure.Repositories;
using ParcelTracking.Infrastructure.Services;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authentication;
using ParcelTracking.Api.Authentication;
using Scalar.AspNetCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Validators;

var builder = WebApplication.CreateBuilder(args);


// EF Core
builder.Services.AddDbContext<ParcelTrackingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", options => { });

builder.Services.AddAuthorization();

// OpenAPI
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Parcel Tracking API",
            Version = "v1",
            Description = "A carrier-style parcel tracking REST API"
        };
        return Task.CompletedTask;
    });

    options.AddDocumentTransformer((document, context, ct) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["ApiKey"] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header,
                Description = "API key passed in the X-Api-Key header"
            };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", document, null)] = new List<string>()
            });

        return Task.CompletedTask;
    });
});

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateAddressRequestValidator>();

// Problem Details (RFC 7807)
builder.Services.AddProblemDetails();

// Application Services
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();

// Parcel Registration
builder.Services.AddScoped<IParcelRepository, ParcelRepository>();
builder.Services.AddSingleton<ITrackingNumberGenerator, TrackingNumberGenerator>();
builder.Services.AddSingleton<IDeliveryEstimator, DeliveryEstimator>();
builder.Services.AddScoped<IParcelRegistrationService, ParcelRegistrationService>();
builder.Services.AddScoped<IParcelRetrievalService, ParcelRetrievalService>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Seed database if --seed argument is provided
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
    await DataSeeder.SeedAsync(db);
    Console.WriteLine("Database seeded successfully.");
    return;
}

// Configure the HTTP request pipeline.
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();