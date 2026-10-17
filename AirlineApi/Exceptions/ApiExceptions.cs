using Microsoft.AspNetCore.Http;

namespace AirlineApi.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

public sealed class BadRequestException : ApiException
{
    public BadRequestException(string message)
        : base(StatusCodes.Status400BadRequest, "Некорректный запрос", message)
    {
    }
}

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message)
        : base(StatusCodes.Status401Unauthorized, "Требуется аутентификация", message)
    {
    }
}

public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string message)
        : base(StatusCodes.Status403Forbidden, "Доступ запрещён", message)
    {
    }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, "Не найдено", message)
    {
    }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Конфликт", message)
    {
    }
}
