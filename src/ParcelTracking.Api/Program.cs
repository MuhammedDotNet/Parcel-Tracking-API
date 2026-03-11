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
using ParcelTracking.Api.ExceptionHandlers;
using Asp.Versioning;
using ParcelTracking.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using ParcelTracking.Api.Middleware;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);


// EF Core
builder.Services.AddDbContext<ParcelTrackingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", options => { });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    {
        options.AddPolicy("Frontend", policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader());
    }
    else
    {
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins("https://tracking.example.com")
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                  .WithHeaders("Content-Type", "Authorization", "X-Api-Key")
                  .WithExposedHeaders("X-Pagination", "X-Request-Id", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset"));
    }
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// OpenAPI
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Parcel Tracking API",
            Version = "v1",
            Description = "Production-grade REST API for parcel registration, tracking, and delivery management",
            Contact = new OpenApiContact
            {
                Name = "API Support",
                Email = "eloqmens@gmail.com"
            }
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

    // Include XML comments for Swagger documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.AddSchemaTransformer((schema, context, ct) =>
        {
            // XML comments are automatically included by the OpenAPI generator
            return Task.CompletedTask;
        });
    }
});

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateAddressRequestValidator>();

// Problem Details (RFC 7807)
builder.Services.AddProblemDetails();

// Exception Handlers
builder.Services.AddExceptionHandler<TerminalStateExceptionHandler>();

// Response Caching
builder.Services.AddResponseCaching();

// Application Services
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();

// Parcel Registration
builder.Services.AddScoped<IParcelRepository, ParcelRepository>();
builder.Services.AddSingleton<ITrackingNumberGenerator, TrackingNumberGenerator>();
builder.Services.AddSingleton<IDeliveryEstimator, DeliveryEstimator>();
builder.Services.AddScoped<IParcelRegistrationService, ParcelRegistrationService>();
builder.Services.AddScoped<IParcelRetrievalService, ParcelRetrievalService>();

// Tracking Events
builder.Services.AddScoped<ITrackingService, TrackingService>();

// Parcel Status Lifecycle
builder.Services.AddScoped<IParcelStatusService, ParcelStatusService>();
builder.Services.AddScoped<IParcelService, ParcelService>();

// Delivery Confirmation
builder.Services.AddScoped<IDeliveryConfirmationRepository, DeliveryConfirmationRepository>();
builder.Services.AddScoped<IDeliveryConfirmationService, DeliveryConfirmationService>();

// Exception Handling & Retry
builder.Services.AddScoped<IExceptionService, ExceptionService>();

// Delivery Estimation
builder.Services.AddSingleton<ITimeZoneResolver, SimpleTimeZoneResolver>();
builder.Services.AddScoped<IDeliveryEstimationService, DeliveryEstimationService>();

// Analytics
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// Rate Limiting
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("PerClient", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                title = "Too Many Requests",
                status = 429,
                detail = "Rate limit exceeded. Please try again later.",
                retryAfter = 60
            }, ct);
        };
    });
}

// Controllers
builder.Services.AddControllers(options =>
{
    // Cache profiles for analytics endpoints
    options.CacheProfiles.Add("Analytics", new Microsoft.AspNetCore.Mvc.CacheProfile
    {
        Duration = 600,  // 10 minutes
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any,
        VaryByQueryKeys = new[] { "from", "to" }
    });

    options.CacheProfiles.Add("AnalyticsShort", new Microsoft.AspNetCore.Mvc.CacheProfile
    {
        Duration = 300,  // 5 minutes
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any,
        VaryByQueryKeys = new[] { "from", "to" }
    });

    options.CacheProfiles.Add("RealTime", new Microsoft.AspNetCore.Mvc.CacheProfile
    {
        Duration = 60,  // 1 minute
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any
    });
})
    .AddNewtonsoftJson();

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
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Request logging middleware - runs first to capture all requests
app.UseMiddleware<RequestLoggingMiddleware>();

// Rewrite unversioned API requests to v1 implicitly
app.UseRewriter(new Microsoft.AspNetCore.Rewrite.RewriteOptions()
    .AddRewrite(@"^api/(?!v\d+/?)(.*)", "api/v1/$1", skipRemainingRules: true));

app.UseResponseCaching();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
if (!app.Environment.IsEnvironment("Testing"))
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
var controllers = app.MapControllers();
if (!app.Environment.IsEnvironment("Testing"))
    controllers.RequireRateLimiting("PerClient");

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // No checks, just confirms process is running
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
}).DisableRateLimiting();

app.Run();

static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description ?? string.Empty,
            durationMs = entry.Value.Duration.TotalMilliseconds
        }),
        totalDurationMs = report.TotalDuration.TotalMilliseconds
    };

    await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}