using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.Problem(
                title: "Некорректный запрос",
                detail: "Тело запроса отсутствует или имеет неверный формат.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        if (!Validator.TryValidateObject(model, validationContext, results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(result =>
                {
                    var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
                    return members.Select(member => (member, result.ErrorMessage ?? "Некорректное значение."));
                })
                .GroupBy(item => item.member, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Item2).Distinct().ToArray());
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
