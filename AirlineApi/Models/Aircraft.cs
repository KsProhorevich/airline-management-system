using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirlineApi.Models;

[Table("Aircraft")]
public sealed class Aircraft
{
    public static readonly Guid FallbackId = Guid.Parse("22222222-2222-2222-2222-222222222201");

    public const string FallbackRegistration = "RA-0001";

    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("Registration")]
    public string Registration { get; set; } = "";

    [Required]
    [MaxLength(80)]
    public string Model { get; set; } = "";

    [Range(1, 500)]
    public int SeatCapacity { get; set; }

    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
}
