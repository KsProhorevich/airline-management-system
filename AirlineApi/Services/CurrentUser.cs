using System.Security.Claims;
using AirlineApi.Exceptions;
using AirlineApi.Models;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirstValue("role");

    public bool IsDispatcher => string.Equals(Role, Roles.Dispatcher, StringComparison.Ordinal);

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("В токене отсутствует идентификатор пользователя.");
}
