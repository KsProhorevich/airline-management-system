using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AirlineApi.Configuration;
using AirlineApi.DTOs.Auth;
using AirlineApi.Exceptions;
using AirlineApi.Models;
using AirlineApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AirlineApi.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthService> _logger;
    private readonly string _dummyPasswordHash;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwt = jwt.Value;
        _logger = logger;
        _dummyPasswordHash = _passwordHasher.HashPassword(new User(), "not-a-real-password");
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Role)
            && !string.Equals(request.Role.Trim(), Roles.Passenger, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Регистрация не позволяет выбрать роль Dispatcher. Новые пользователи получают роль Passenger.");
        }

        var email = NormalizeEmail(request.Email);
        var fullName = request.FullName.Trim();
        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            _logger.LogInformation("Registration rejected because the email is already used");
            throw new ConflictException("Пользователь с таким email уже зарегистрирован.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            Role = Roles.Passenger,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Registered user {UserId} with role {Role}", user.Id, user.Role);
        return IssueToken(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            _passwordHasher.VerifyHashedPassword(new User(), _dummyPasswordHash, request.Password);
            _logger.LogInformation("Login failed");
            throw new UnauthorizedException("Неверный email или пароль.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded))
        {
            _logger.LogInformation("Login failed for user {UserId}", user.Id);
            throw new UnauthorizedException("Неверный email или пароль.");
        }

        _logger.LogInformation("User {UserId} logged in with role {Role}", user.Id, user.Role);
        return IssueToken(user);
    }

    private AuthResponse IssueToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresAtUtc = expires,
            User = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role
            }
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
