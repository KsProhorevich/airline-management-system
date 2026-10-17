using AirlineApi.DTOs.Aircraft;
using AirlineApi.DTOs.Common;
using AirlineApi.Exceptions;
using AirlineApi.Repositories;

namespace AirlineApi.Services;

public sealed class AircraftService : IAircraftService
{
    private readonly IAircraftRepository _aircraft;
    private readonly IFlightRepository _flights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AircraftService> _logger;

    public AircraftService(
        IAircraftRepository aircraft,
        IFlightRepository flights,
        IUnitOfWork unitOfWork,
        ILogger<AircraftService> logger)
    {
        _aircraft = aircraft;
        _flights = flights;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResponse<AircraftResponse>> ListAsync(AircraftListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page ?? PaginationRules.DefaultPage;
        var pageSize = query.PageSize ?? PaginationRules.DefaultPageSize;
        PaginationRules.Validate(page, pageSize);
        var (items, total) = await _aircraft.SearchAsync(page, pageSize, cancellationToken);
        return new PagedResponse<AircraftResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = PaginationRules.TotalPages(total, pageSize)
        };
    }

    public async Task<AircraftResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var aircraft = await _aircraft.GetByIdAsync(id, tracking: false, cancellationToken);
        if (aircraft is null)
        {
            throw new NotFoundException("Воздушное судно не найдено.");
        }

        return ToResponse(aircraft);
    }

    public async Task<AircraftResponse> CreateAsync(UpsertAircraftRequest request, CancellationToken cancellationToken)
    {
        var registration = Normalize(request.Registration);
        var model = request.Model.Trim();
        if (await _aircraft.RegistrationExistsAsync(registration, null, cancellationToken))
        {
            throw new ConflictException("Судно с таким регистрационным номером уже существует.");
        }

        var aircraft = new Models.Aircraft
        {
            Id = Guid.NewGuid(),
            Registration = registration,
            Model = model,
            SeatCapacity = request.SeatCapacity
        };
        await _aircraft.AddAsync(aircraft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Aircraft {AircraftId} created", aircraft.Id);
        return ToResponse(aircraft);
    }

    public async Task<AircraftResponse> UpdateAsync(Guid id, UpsertAircraftRequest request, CancellationToken cancellationToken)
    {
        var aircraft = await _aircraft.GetByIdAsync(id, tracking: true, cancellationToken);
        if (aircraft is null)
        {
            throw new NotFoundException("Воздушное судно не найдено.");
        }

        var registration = Normalize(request.Registration);
        if (await _aircraft.RegistrationExistsAsync(registration, aircraft.Id, cancellationToken))
        {
            throw new ConflictException("Судно с таким регистрационным номером уже существует.");
        }

        if (request.SeatCapacity < await _flights.MaxTotalSeatsAsync(aircraft.Id, cancellationToken))
        {
            throw new BadRequestException("Вместимость нельзя уменьшить ниже числа мест уже созданных рейсов этого судна.");
        }

        aircraft.Registration = registration;
        aircraft.Model = request.Model.Trim();
        aircraft.SeatCapacity = request.SeatCapacity;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Aircraft {AircraftId} updated", aircraft.Id);
        return ToResponse(aircraft);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var aircraft = await _aircraft.GetByIdAsync(id, tracking: true, cancellationToken);
        if (aircraft is null)
        {
            throw new NotFoundException("Воздушное судно не найдено.");
        }

        if (aircraft.Id == Models.Aircraft.FallbackId)
        {
            throw new ConflictException("Резервное судно удалять нельзя: на него опираются рейсы без явного выбора.");
        }

        if (await _aircraft.HasFlightsAsync(aircraft.Id, cancellationToken))
        {
            throw new ConflictException("Нельзя удалить судно, пока на него назначены рейсы.");
        }

        _aircraft.Remove(aircraft);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Aircraft {AircraftId} deleted", aircraft.Id);
    }

    private static string Normalize(string registration) => registration.Trim().ToUpperInvariant();

    private static AircraftResponse ToResponse(Models.Aircraft aircraft) => new()
    {
        Id = aircraft.Id,
        Registration = aircraft.Registration,
        Model = aircraft.Model,
        SeatCapacity = aircraft.SeatCapacity
    };
}
