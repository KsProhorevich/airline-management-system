using System.Text.Json;
using AirlineApi.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Middleware;

public readonly record struct MappedProblem(int Status, string Title, string Detail, bool LogAsError);

public static class ExceptionProblemMapper
{
    public static MappedProblem Map(Exception exception)
    {
        return exception switch
        {
            ApiException api => new MappedProblem(api.StatusCode, api.Title, api.Message, false),
            BadHttpRequestException => new MappedProblem(
                StatusCodes.Status400BadRequest,
                "Некорректный запрос",
                "Тело запроса отсутствует или имеет неверный формат.",
                false),
            JsonException => new MappedProblem(
                StatusCodes.Status400BadRequest,
                "Некорректный запрос",
                "Тело запроса содержит некорректный JSON.",
                false),
            DbUpdateConcurrencyException => new MappedProblem(
                StatusCodes.Status409Conflict,
                "Конфликт",
                "Данные уже были изменены другим запросом. Повторите операцию.",
                false),
            DbUpdateException db when IsUniqueViolation(db) => new MappedProblem(
                StatusCodes.Status409Conflict,
                "Конфликт",
                "Запись с такими уникальными данными уже существует.",
                false),
            _ => new MappedProblem(
                StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера",
                "Произошла непредвиденная ошибка. Повторите запрос позже.",
                true)
        };
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite && sqlite.SqliteExtendedErrorCode == 2067)
            {
                return true;
            }
        }

        return false;
    }
}
