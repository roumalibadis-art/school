using Microsoft.Extensions.Hosting;
using Serilog;
using USTHBStudy.API.Extensions;
using USTHBStudy.API.Middleware;
using USTHBStudy.Application;
using USTHBStudy.Infrastructure;

// A plain (non-reloadable) startup logger. Not CreateBootstrapLogger(): the reloadable logger
// is frozen by AddSerilog and cannot be re-frozen when multiple hosts share a test process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // AddSerilog (rather than Host.UseSerilog) builds a logger scoped to the DI container, which
    // avoids the "logger is already frozen" clash when several hosts run in one test process.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName());

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);

    var app = builder.Build();

    await app.ApplyStartupAsync();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSecurityHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in integration tests.</summary>
public partial class Program;
