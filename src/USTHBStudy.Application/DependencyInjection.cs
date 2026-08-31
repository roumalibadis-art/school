namespace USTHBStudy.Application;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using USTHBStudy.Application.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Keep built-in validation messages in English regardless of server locale;
        // user-facing localization is the frontend's job (PRD §72).
        FluentValidation.ValidatorOptions.Global.LanguageManager.Culture =
            System.Globalization.CultureInfo.GetCultureInfo("en");

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
