using System.Text.RegularExpressions;
using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Flights;
using AirlineApi.Exceptions;
using AirlineApi.Models;
using AirlineApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Services;

public sealed class FlightService : IFlightService
{
    private static readonly Regex AirportRegex = new("^[A-Z]{3}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FlightNumberRegex = new("^[A-Z]{2}\\d{1,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "departureTime",
        "arrivalTime",
        "flightNumber"
    };

    private readonly IFlightRepository _flights;
    private readonly IBookingRepository _bookings;
    private readonly IAircraftRepository _aircraft;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FlightService> _logger;

    public FlightService(
        IFlightRepository flights,
        IBookingRepository bookings,
        IAircraftRepository aircraft,
        IUnitOfWork unitOfWork,
        ILogger<FlightService> logger)
    {
        _flights = flights;
        _bookings = bookings;
        _aircraft = aircraft;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResponse<FlightResponse>> ListAsync(FlightListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page ?? PaginationRules.DefaultPage;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        PaginationRules.Validate(page, pageSize);
        var criteria = new FlightSearchCriteria(
            page,
            pageSize,
            ParseAirportFilter(query.DepartureAirport, "Аэропорт вылета"),
            ParseAirportFilter(query.ArrivalAirport, "Аэропорт прилёта"),
            ParseStatus(query.Status),
            ParseSort(query.SortBy),
            ParseDescending(query.SortDir),
            query.AircraftId is Guid aircraftId && aircraftId != Guid.Empty ? aircraftId : null);
        var (items, total) = await _flights.SearchAsync(criteria, cancellationToken);
        _logger.LogInformation(
            "Listed flights page {Page} size {PageSize} returned {Count} of {Total}",
            page,
            pageSize,
            items.Count,
            total);
        return new PagedResponse<FlightResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = PaginationRules.TotalPages(total, pageSize)
        };
    }

    public async Task<FlightResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var flight = await _flights.GetByIdAsync(id, tracking: false, cancellationToken);
        if (flight is null)
        {
            throw new NotFoundException("Рейс не найден.");
        }

        return ToResponse(flight);
    }

    public async Task<FlightResponse> CreateAsync(CreateFlightRequest request, CancellationToken cancellationToken)
    {
        var draft = Normalize(request.FlightNumber, request.DepartureAirport, request.ArrivalAirport, request.DepartureTime, request.ArrivalTime, request.TotalSeats);
        if (await _flights.FlightNumberExistsAsync(draft.FlightNumber, null, cancellationToken))
        {
            throw new ConflictException("Рейс с таким номером уже существует.");
        }

        var aircraft = await ResolveAircraftAsync(request.AircraftId, draft.TotalSeats, cancellationToken);
        var flight = new Flight
        {
            Id = Guid.NewGuid(),
            FlightNumber = draft.FlightNumber,
            DepartureAirport = draft.DepartureAirport,
            ArrivalAirport = draft.ArrivalAirport,
            DepartureTime = draft.DepartureTime,
            ArrivalTime = draft.ArrivalTime,
            Status = request.Status,
            TotalSeats = draft.TotalSeats,
            AvailableSeats = draft.TotalSeats,
            Version = 0,
            AircraftId = aircraft.Id,
            Aircraft = aircraft
        };
        await _flights.AddAsync(flight, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Flight {FlightId} created with number {FlightNumber}", flight.Id, flight.FlightNumber);
        return ToResponse(flight);
    }

    public async Task<FlightResponse> UpdateAsync(Guid id, UpdateFlightRequest request, CancellationToken cancellationToken)
    {
        var flight = await _flights.GetByIdAsync(id, tracking: true, cancellationToken);
        if (flight is null)
        {
            throw new NotFoundException("Рейс не найден.");
        }

        var draft = Normalize(request.FlightNumber, request.DepartureAirport, request.ArrivalAirport, request.DepartureTime, request.ArrivalTime, request.TotalSeats);
        if (await _flights.FlightNumberExistsAsync(draft.FlightNumber, flight.Id, cancellationToken))
        {
            throw new ConflictException("Рейс с таким номером уже существует.");
        }

        var booked = flight.TotalSeats - flight.AvailableSeats;
        if (draft.TotalSeats < booked)
        {
            throw new BadRequestException($"Нельзя установить {draft.TotalSeats} мест: уже занято {booked}.");
        }

        var aircraft = await ResolveAircraftAsync(request.AircraftId ?? flight.AircraftId, draft.TotalSeats, cancellationToken);
        flight.FlightNumber = draft.FlightNumber;
        flight.DepartureAirport = draft.DepartureAirport;
        flight.ArrivalAirport = draft.ArrivalAirport;
        flight.DepartureTime = draft.DepartureTime;
        flight.ArrivalTime = draft.ArrivalTime;
        flight.Status = request.Status;
        flight.TotalSeats = draft.TotalSeats;
        flight.AvailableSeats = draft.TotalSeats - booked;
        flight.AircraftId = aircraft.Id;
        flight.Aircraft = aircraft;
        flight.Version++;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrent flight update {FlightId}", flight.Id);
            throw new ConflictException("Рейс был изменён другим запросом. Повторите операцию.");
        }

        _logger.LogInformation("Flight {FlightId} updated", flight.Id);
        return ToResponse(flight);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var flight = await _flights.GetByIdAsync(id, tracking: true, cancellationToken);
        if (flight is null)
        {
            throw new NotFoundException("Рейс не найден.");
        }

        if (await _bookings.ExistsForFlightAsync(flight.Id, cancellationToken))
        {
            throw new ConflictException("Нельзя удалить рейс, пока на него есть бронирования, включая отменённые.");
        }

        _flights.Remove(flight);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Flight {FlightId} deleted", flight.Id);
    }

    private static FlightSortField ParseSort(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return FlightSortField.DepartureTime;
        }

        if (sortBy.Length > 32 || !SortFields.Contains(sortBy.Trim()))
        {
            throw new BadRequestException("Поле сортировки должно быть одним из: departureTime, arrivalTime, flightNumber.");
        }

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "arrivaltime" => FlightSortField.ArrivalTime,
            "flightnumber" => FlightSortField.FlightNumber,
            _ => FlightSortField.DepartureTime
        };
    }

    private static bool ParseDescending(string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortDir) || sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new BadRequestException("Направление сортировки должно быть asc или desc.");
    }

    private static string? ParseAirportFilter(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!AirportRegex.IsMatch(normalized))
        {
            throw new BadRequestException($"{fieldName}: код IATA из трёх латинских букв.");
        }

        return normalized;
    }

    private static FlightStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 32
            || int.TryParse(trimmed, out _)
            || !Enum.TryParse<FlightStatus>(trimmed, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            throw new BadRequestException("Статус рейса должен быть одним из: Scheduled, Delayed, Boarding, Departed, Arrived, Cancelled.");
        }

        return status;
    }

    private static NormalizedFlight Normalize(
        string flightNumber,
        string departureAirport,
        string arrivalAirport,
        DateTime departureTime,
        DateTime arrivalTime,
        int totalSeats)
    {
        var number = flightNumber.Trim().ToUpperInvariant();
        var departure = departureAirport.Trim().ToUpperInvariant();
        var arrival = arrivalAirport.Trim().ToUpperInvariant();
        if (!FlightNumberRegex.IsMatch(number))
        {
            throw new BadRequestException("Номер рейса: две латинские буквы и от 1 до 4 цифр.");
        }

        if (!AirportRegex.IsMatch(departure) || !AirportRegex.IsMatch(arrival))
        {
            throw new BadRequestException("Аэропорты задаются кодом IATA из трёх латинских букв.");
        }

        if (departure == arrival)
        {
            throw new BadRequestException("Аэропорт вылета и аэропорт прилёта должны различаться.");
        }

        var departureUtc = AsUtc(departureTime);
        var arrivalUtc = AsUtc(arrivalTime);
        if (departureUtc == default || arrivalUtc == default || arrivalUtc <= departureUtc)
        {
            throw new BadRequestException("Время прилёта должно быть позже времени вылета.");
        }

        if (totalSeats is < 1 or > 500)
        {
            throw new BadRequestException("Число мест должно быть от 1 до 500.");
        }

        return new NormalizedFlight(number, departure, arrival, departureUtc, arrivalUtc, totalSeats);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static FlightResponse ToResponse(Flight flight) => new()
    {
        Id = flight.Id,
        FlightNumber = flight.FlightNumber,
        DepartureAirport = flight.DepartureAirport,
        ArrivalAirport = flight.ArrivalAirport,
        DepartureTime = DateTime.SpecifyKind(flight.DepartureTime, DateTimeKind.Utc),
        ArrivalTime = DateTime.SpecifyKind(flight.ArrivalTime, DateTimeKind.Utc),
        Status = flight.Status.ToString(),
        TotalSeats = flight.TotalSeats,
        AvailableSeats = flight.AvailableSeats,
        AircraftId = flight.AircraftId,
        AircraftRegistration = flight.Aircraft?.Registration ?? "",
        AircraftModel = flight.Aircraft?.Model ?? ""
    };

    private async Task<Aircraft> ResolveAircraftAsync(Guid? requestedId, int totalSeats, CancellationToken cancellationToken)
    {
        Aircraft? aircraft;
        if (requestedId is Guid id && id != Guid.Empty)
        {
            aircraft = await _aircraft.GetByIdAsync(id, tracking: true, cancellationToken);
            if (aircraft is null)
            {
                throw new NotFoundException("Воздушное судно не найдено.");
            }
        }
        else
        {
            var fallback = await _aircraft.GetByIdAsync(Aircraft.FallbackId, tracking: true, cancellationToken);
            if (fallback is null)
            {
                var first = await _aircraft.GetFirstAsync(cancellationToken);
                fallback = first is null
                    ? null
                    : await _aircraft.GetByIdAsync(first.Id, tracking: true, cancellationToken);
            }

            aircraft = fallback;
            if (aircraft is null)
            {
                throw new BadRequestException("Нет воздушного судна. Создайте его или передайте aircraftId.");
            }
        }

        if (totalSeats > aircraft.SeatCapacity)
        {
            throw new BadRequestException($"Число мест рейса не может превышать вместимость судна ({aircraft.SeatCapacity}).");
        }

        return aircraft;
    }

    private sealed record NormalizedFlight(
        string FlightNumber,
        string DepartureAirport,
        string ArrivalAirport,
        DateTime DepartureTime,
        DateTime ArrivalTime,
        int TotalSeats);
}
