namespace USTHBStudy.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Academic;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Documents;
using USTHBStudy.Infrastructure.Academic;
using USTHBStudy.Infrastructure.Auth;
using USTHBStudy.Infrastructure.Documents;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence;
using USTHBStudy.Infrastructure.Persistence.Interceptors;
using USTHBStudy.Infrastructure.Services;
using USTHBStudy.Infrastructure.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        AddOptions(services, configuration);
        AddPersistence(services, configuration);
        AddIdentity(services);
        AddStorage(services, configuration);

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<DbSeeder>();

        services.AddSingleton<IPdfProcessor, PdfiumPdfProcessor>();
        services.AddScoped<IDocumentService, DocumentService>();

        AddAcademic(services);

        return services;
    }

    private static void AddAcademic(IServiceCollection services)
    {
        services.AddScoped<IUniversityService, UniversityService>();
        services.AddScoped<IFacultyService, FacultyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IDomainService, DomainService>();
        services.AddScoped<ISpecialtyService, SpecialtyService>();
        services.AddScoped<ILevelService, LevelService>();
        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IModuleService, ModuleService>();
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Secret) && o.Secret.Length >= 32,
                "Jwt:Secret must be configured with at least 32 characters (use user-secrets or environment variables).")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 1440, "Jwt:AccessTokenMinutes must be between 1 and 1440.")
            .Validate(o => o.RefreshTokenDays is > 0 and <= 90, "Jwt:RefreshTokenDays must be between 1 and 90.")
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddOptions<DocumentOptions>()
            .Bind(configuration.GetSection(DocumentOptions.SectionName));
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it via user-secrets or environment variables.");

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 36)),
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());

            // Every academic entity carries the same soft-delete filter and FKs are Restrict, so the
            // required-navigation/query-filter interaction warning is expected here.
            options.ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        // Token providers (email confirmation / password reset / 2FA) are added in Phase 5/7
        // together with the email flows that use them.
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetSection(FileStorageOptions.SectionName)["Provider"] ?? "Local";

        if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            return;
        }

        throw new InvalidOperationException(
            $"Storage provider '{provider}' is not available yet. Use 'Local'. The S3-compatible provider lands in Phase 3.");
    }
}
