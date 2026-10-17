using AirlineApi.Models;

namespace AirlineApi.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchPassengersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}
