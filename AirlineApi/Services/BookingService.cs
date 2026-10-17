using AirlineApi.DTOs.Bookings;
using AirlineApi.DTOs.Common;
using AirlineApi.Exceptions;
using AirlineApi.Models;
using AirlineApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Services;

public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookings;
    private readonly IFlightRepository _flights;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookings,
        IFlightRepository flights,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ILogger<BookingService> logger)
    {
        _bookings = bookings;
        _flights = flights;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<BookingResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await _bookings.GetReadOnlyAsync(id, cancellationToken);
        if (booking is null)
        {
            throw new NotFoundException("Бронирование не найдено.");
        }

        EnsureCanAccess(booking.PassengerId, "Нельзя просматривать чужое бронирование.");
        return ToResponse(booking);
    }

    public async Task<IReadOnlyList<BookingResponse>> ListMineAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var items = await _bookings.ListForPassengerAsync(userId, cancellationToken);
        _logger.LogInformation("Listed {Count} bookings for user {UserId}", items.Count, userId);
        return items.Select(ToResponse).ToList();
    }

    public async Task<PagedResponse<BookingResponse>> ListAsync(BookingListQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsDispatcher)
        {
            throw new ForbiddenException("Список всех бронирований доступен только диспетчеру.");
        }

        var page = query.Page ?? PaginationRules.DefaultPage;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        PaginationRules.Validate(page, pageSize);
        var criteria = new BookingSearchCriteria(
            page,
            pageSize,
            query.FlightId == Guid.Empty ? null : query.FlightId,
            query.PassengerId == Guid.Empty ? null : query.PassengerId,
            ParseStatus(query.Status));
        var (items, total) = await _bookings.SearchAsync(criteria, cancellationToken);
        return new PagedResponse<BookingResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = PaginationRules.TotalPages(total, pageSize)
        };
    }

    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (request.FlightId == Guid.Empty)
        {
            throw new BadRequestException("Укажите рейс.");
        }

        if (request.SeatCount is < 1 or > 9)
        {
            throw new BadRequestException("Число мест должно быть от 1 до 9.");
        }

        var passengerId = ResolvePassengerId(request);
        var passenger = await _users.GetByIdAsync(passengerId, cancellationToken);
        if (passenger is null)
        {
            throw new NotFoundException("Пассажир не найден.");
        }

        if (!string.Equals(passenger.Role, Roles.Passenger, StringComparison.Ordinal))
        {
            throw new BadRequestException("Бронирование можно оформить только на пользователя с ролью Passenger.");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var flight = await _flights.GetByIdAsync(request.FlightId, tracking: true, cancellationToken);
            if (flight is null)
            {
                throw new NotFoundException("Рейс не найден.");
            }

            EnsureBookable(flight, DateTime.UtcNow);
            if (await _bookings.HasActiveBookingAsync(passenger.Id, flight.Id, cancellationToken))
            {
                throw new ConflictException("У пассажира уже есть активное бронирование на этот рейс.");
            }

            if (flight.AvailableSeats < request.SeatCount)
            {
                throw new ConflictException("Недостаточно свободных мест.");
            }

            flight.AvailableSeats -= request.SeatCount;
            flight.Version++;
            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                FlightId = flight.Id,
                PassengerId = passenger.Id,
                CreatedAt = DateTime.UtcNow,
                Status = BookingStatus.Confirmed,
                SeatCount = request.SeatCount
            };
            await _bookings.AddAsync(booking, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Booking {BookingId} created for flight {FlightId} by passenger {PassengerId}",
                booking.Id,
                flight.Id,
                passenger.Id);
            booking.Flight = flight;
            booking.Passenger = passenger;
            return ToResponse(booking);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrent seat update for flight {FlightId}", request.FlightId);
            throw new ConflictException("Не удалось забронировать места, потому что данные рейса уже изменились. Повторите запрос.");
        }
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var booking = await _bookings.GetTrackedAsync(id, cancellationToken);
        if (booking is null)
        {
            throw new NotFoundException("Бронирование не найдено.");
        }

        EnsureCanAccess(booking.PassengerId, "Нельзя отменить чужое бронирование.");
        if (booking.Status == BookingStatus.Cancelled)
        {
            throw new ConflictException("Бронирование уже отменено.");
        }

        if (booking.Flight is null)
        {
            throw new ConflictException("Рейс бронирования не найден.");
        }

        var restored = booking.Flight.AvailableSeats + booking.SeatCount;
        if (restored > booking.Flight.TotalSeats)
        {
            throw new ConflictException("Нельзя вернуть места: это превысило бы вместимость рейса.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.Flight.AvailableSeats = restored;
        booking.Flight.Version++;
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrent update while cancelling booking {BookingId}", booking.Id);
            throw new ConflictException("Не удалось отменить бронирование, потому что данные рейса уже изменились. Повторите запрос.");
        }

        _logger.LogInformation("Booking {BookingId} cancelled by user {UserId}", booking.Id, _currentUser.UserId);
    }

    private Guid ResolvePassengerId(CreateBookingRequest request)
    {
        if (!_currentUser.IsDispatcher)
        {
            var userId = _currentUser.RequireUserId();
            if (request.PassengerId is Guid supplied && supplied != userId)
            {
                _logger.LogInformation("Ignored client-supplied passenger id for user {UserId}", userId);
            }

            return userId;
        }

        if (request.PassengerId is not Guid passengerId || passengerId == Guid.Empty)
        {
            throw new BadRequestException(
                "Диспетчер должен указать passengerId существующего пассажира. Идентификатор из токена диспетчера для бронирования не используется.");
        }

        return passengerId;
    }

    private void EnsureCanAccess(Guid passengerId, string message)
    {
        if (_currentUser.IsDispatcher)
        {
            return;
        }

        if (passengerId != _currentUser.RequireUserId())
        {
            _logger.LogWarning("User {UserId} attempted to access another passenger booking", _currentUser.UserId);
            throw new ForbiddenException(message);
        }
    }

    private static void EnsureBookable(Flight flight, DateTime utcNow)
    {
        if (flight.Status is not (FlightStatus.Scheduled or FlightStatus.Delayed or FlightStatus.Boarding))
        {
            throw new ConflictException("Бронирование недоступно для рейса в текущем статусе.");
        }

        if (flight.DepartureTime <= utcNow)
        {
            throw new ConflictException("Бронирование недоступно: время вылета уже наступило.");
        }
    }

    private static BookingStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 32
            || int.TryParse(trimmed, out _)
            || !Enum.TryParse<BookingStatus>(trimmed, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            throw new BadRequestException("Статус бронирования должен быть Confirmed или Cancelled.");
        }

        return status;
    }

    private static BookingResponse ToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        FlightId = booking.FlightId,
        FlightNumber = booking.Flight?.FlightNumber ?? "",
        PassengerId = booking.PassengerId,
        PassengerEmail = booking.Passenger?.Email ?? "",
        PassengerName = booking.Passenger?.FullName ?? "",
        CreatedAt = DateTime.SpecifyKind(booking.CreatedAt, DateTimeKind.Utc),
        Status = booking.Status.ToString(),
        SeatCount = booking.SeatCount
    };
}
