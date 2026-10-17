using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirlineApi.Models;

[Table("Flights")]
public sealed class Flight
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(8)]
    public string FlightNumber { get; set; } = "";

    [Required]
    [MaxLength(3)]
    public string DepartureAirport { get; set; } = "";

    [Required]
    [MaxLength(3)]
    public string ArrivalAirport { get; set; } = "";

    public DateTime DepartureTime { get; set; }

    public DateTime ArrivalTime { get; set; }

    public FlightStatus Status { get; set; }

    public int TotalSeats { get; set; }

    public int AvailableSeats { get; set; }

    public int Version { get; set; }

    [ForeignKey(nameof(Aircraft))]
    public Guid AircraftId { get; set; }

    public Aircraft Aircraft { get; set; } = null!;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
