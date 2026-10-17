using AirlineApi.DTOs.Bookings;
using AirlineApi.Exceptions;
using AirlineApi.Models;
using AirlineApi.Repositories;
using AirlineApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AirlineApi.Tests;

public sealed class BookingServiceTests
{
    private readonly Guid _passengerId = Guid.NewGuid();
    private readonly Guid _flightId = Guid.NewGuid();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IFlightRepository> _flights = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public BookingServiceTests()
    {
        _unitOfWork.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FakeTransaction());
        _unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _currentUser.SetupGet(item => item.UserId).Returns(_passengerId);
        _currentUser.Setup(item => item.RequireUserId()).Returns(_passengerId);
        _users.Setup(item => item.GetByIdAsync(_passengerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Passenger());
    }

    [Fact]
    public async Task Create_reserves_seats_when_enough_are_available()
    {
        var flight = BookableFlight(available: 5);
        _flights.Setup(item => item.GetByIdAsync(_flightId, true, It.IsAny<CancellationToken>())).ReturnsAsync(flight);
        _bookings.Setup(item => item.HasActiveBookingAsync(_passengerId, _flightId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateService(dispatcher: false).CreateAsync(Request(seats: 2), CancellationToken.None);

        Assert.Equal(3, flight.AvailableSeats);
        Assert.Equal(2, result.SeatCount);
        Assert.Equal(_passengerId, result.PassengerId);
        _bookings.Verify(item => item.AddAsync(It.Is<Booking>(booking => booking.SeatCount == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_rejects_booking_when_seats_are_insufficient()
    {
        var flight = BookableFlight(available: 1);
        _flights.Setup(item => item.GetByIdAsync(_flightId, true, It.IsAny<CancellationToken>())).ReturnsAsync(flight);
        _bookings.Setup(item => item.HasActiveBookingAsync(_passengerId, _flightId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(dispatcher: false).CreateAsync(Request(seats: 2), CancellationToken.None));

        Assert.Contains("Недостаточно", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, flight.AvailableSeats);
        _bookings.Verify(item => item.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_booking()
    {
        _flights.Setup(item => item.GetByIdAsync(_flightId, true, It.IsAny<CancellationToken>())).ReturnsAsync(BookableFlight(available: 8));
        _bookings.Setup(item => item.HasActiveBookingAsync(_passengerId, _flightId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(dispatcher: false).CreateAsync(Request(seats: 1), CancellationToken.None));

        _bookings.Verify(item => item.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_uses_authenticated_passenger_and_ignores_supplied_id()
    {
        var supplied = Guid.NewGuid();
        _flights.Setup(item => item.GetByIdAsync(_flightId, true, It.IsAny<CancellationToken>())).ReturnsAsync(BookableFlight(available: 4));
        _bookings.Setup(item => item.HasActiveBookingAsync(_passengerId, _flightId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateService(dispatcher: false).CreateAsync(Request(seats: 1, passengerId: supplied), CancellationToken.None);

        Assert.Equal(_passengerId, result.PassengerId);
        _users.Verify(item => item.GetByIdAsync(supplied, It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(item => item.GetByIdAsync(_passengerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_restores_seats_and_marks_booking_cancelled()
    {
        var flight = BookableFlight(available: 4);
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            FlightId = flight.Id,
            PassengerId = _passengerId,
            SeatCount = 3,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            Flight = flight,
            Passenger = Passenger()
        };
        _bookings.Setup(item => item.GetTrackedAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        await CreateService(dispatcher: false).CancelAsync(booking.Id, CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(7, flight.AvailableSeats);
    }

    [Fact]
    public async Task Create_rejects_flight_that_is_not_bookable()
    {
        var flight = BookableFlight(available: 10);
        flight.Status = FlightStatus.Departed;
        _flights.Setup(item => item.GetByIdAsync(_flightId, true, It.IsAny<CancellationToken>())).ReturnsAsync(flight);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(dispatcher: false).CreateAsync(Request(seats: 1), CancellationToken.None));
    }

    private BookingService CreateService(bool dispatcher)
    {
        _currentUser.SetupGet(item => item.IsDispatcher).Returns(dispatcher);
        return new BookingService(
            _bookings.Object,
            _flights.Object,
            _users.Object,
            _unitOfWork.Object,
            _currentUser.Object,
            NullLogger<BookingService>.Instance);
    }

    private CreateBookingRequest Request(int seats, Guid? passengerId = null) => new()
    {
        FlightId = _flightId,
        SeatCount = seats,
        PassengerId = passengerId
    };

    private Flight BookableFlight(int available) => new()
    {
        Id = _flightId,
        FlightNumber = "SU100",
        DepartureAirport = "SVO",
        ArrivalAirport = "LED",
        DepartureTime = DateTime.UtcNow.AddDays(2),
        ArrivalTime = DateTime.UtcNow.AddDays(2).AddHours(2),
        Status = FlightStatus.Scheduled,
        TotalSeats = 10,
        AvailableSeats = available,
        AircraftId = Aircraft.FallbackId
    };

    private User Passenger() => new()
    {
        Id = _passengerId,
        Email = "passenger@test.local",
        FullName = "Тестовый Пассажир",
        Role = Roles.Passenger,
        PasswordHash = "not-used"
    };

    private sealed class FakeTransaction : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
