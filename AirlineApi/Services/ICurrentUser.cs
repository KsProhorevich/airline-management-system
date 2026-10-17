namespace AirlineApi.Services;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Role { get; }

    bool IsDispatcher { get; }

    Guid RequireUserId();
}
