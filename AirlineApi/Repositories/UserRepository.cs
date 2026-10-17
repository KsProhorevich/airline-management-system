using AirlineApi.Data;
using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _db;

    public UserRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking().AnyAsync(user => user.Email == email, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchPassengersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _db.Users.AsNoTracking().Where(user => user.Role == Roles.Passenger);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(user => user.Email.Contains(term) || user.FullName.ToLower().Contains(term));
        }

        var ordered = query.OrderBy(user => user.FullName).ThenBy(user => user.Email);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _db.Users.Add(user);
        return Task.CompletedTask;
    }
}
