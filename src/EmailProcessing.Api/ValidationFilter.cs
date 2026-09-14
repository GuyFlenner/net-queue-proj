using FluentValidation;

namespace EmailProcessing.Api;

/// <summary>
/// Minimal APIs don't run MVC's auto-validation filters, so validation has to be invoked
/// explicitly — this generic <see cref="IEndpointFilter"/> is that one explicit place,
/// applied per-endpoint with <c>.AddEndpointFilter&lt;ValidationFilter&lt;T&gt;&gt;()</c>.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : notnull
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No argument of type {typeof(T).Name} found on this endpoint.");

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
