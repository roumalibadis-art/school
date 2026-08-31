namespace USTHBStudy.API.Extensions;

using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using USTHBStudy.API.Filters;
using USTHBStudy.API.Services;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;

public static class ApiServiceExtensions
{
    public const string GlobalRateLimitPolicy = "global";
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Validators that live in the API assembly (e.g. multipart form models).
        services.AddValidatorsFromAssembly(typeof(ApiServiceExtensions).Assembly, includeInternalTypes: true);

        services.AddControllers(options => options.Filters.Add<ValidationActionFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value is { Errors.Count: > 0 })
                    .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                        $"{kvp.Key}: {(string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)}"))
                    .ToArray();

                return new BadRequestObjectResult(
                    ApiResponse.Fail("One or more validation errors occurred.", errors));
            };
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "USTHB Study API",
                Version = "v1",
                Description = "Centralized academic resource platform — see PRD.md.",
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token (without the 'Bearer ' prefix).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:3000" };
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        AddRateLimiting(services, configuration);

        return services;
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var globalPerMinute = configuration.GetValue("RateLimiting:PermitPerMinute", 100);
        var authPerMinute = configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        static string PartitionKey(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
