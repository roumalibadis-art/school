namespace USTHBStudy.API.Filters;

using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using USTHBStudy.Application.Common;

/// <summary>
/// Runs any registered <see cref="IValidator{T}"/> against each action argument before the action
/// executes. Failures become a <see cref="ValidationAppException"/> → standard 400 error body (PRD §45).
/// </summary>
public sealed class ValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationActionFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                var errors = result.Errors
                    .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                    .ToArray();
                throw new ValidationAppException(errors);
            }
        }

        await next();
    }
}
