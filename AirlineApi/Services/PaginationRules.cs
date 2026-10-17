using AirlineApi.Exceptions;

namespace AirlineApi.Services;

public static class PaginationRules
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 10;

    public const int MaxPageSize = 50;

    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new BadRequestException("Номер страницы должен быть не меньше 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new BadRequestException($"Размер страницы должен быть от 1 до {MaxPageSize}.");
        }
    }

    public static int TotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
