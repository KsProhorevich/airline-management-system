using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Passengers;
using AirlineApi.Exceptions;
using AirlineApi.Repositories;

namespace AirlineApi.Services;

public interface IPassengerQueryService
{
    Task<PagedResponse<PassengerResponse>> ListAsync(PassengerListQuery query, CancellationToken cancellationToken);
}

public sealed class PassengerQueryService : IPassengerQueryService
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;

    public PassengerQueryService(IUserRepository users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<PagedResponse<PassengerResponse>> ListAsync(PassengerListQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsDispatcher)
        {
            throw new ForbiddenException("Список пассажиров доступен только диспетчеру.");
        }

        if (!string.IsNullOrWhiteSpace(query.Search) && query.Search.Trim().Length > 200)
        {
            throw new BadRequestException("Строка поиска не длиннее 200 символов.");
        }

        var page = query.Page ?? PaginationRules.DefaultPage;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        PaginationRules.Validate(page, pageSize);
        var (items, total) = await _users.SearchPassengersAsync(page, pageSize, query.Search, cancellationToken);
        return new PagedResponse<PassengerResponse>
        {
            Items = items.Select(user => new PassengerResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                CreatedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = PaginationRules.TotalPages(total, pageSize)
        };
    }
}
