using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirlineApi.Models;

[Table("Users")]
public sealed class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = "";

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = "";

    [Required]
    [MaxLength(32)]
    public string Role { get; set; } = Roles.Passenger;

    public DateTime CreatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

