using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirlineApi.Models;

[Table("Bookings")]
public sealed class Booking
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey(nameof(Flight))]
    public Guid FlightId { get; set; }

    public Flight Flight { get; set; } = null!;

    [ForeignKey(nameof(Passenger))]
    public Guid PassengerId { get; set; }

    public User Passenger { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public BookingStatus Status { get; set; }

    [Range(1, 9)]
    public int SeatCount { get; set; }
}
