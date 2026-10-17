using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Aircraft> Aircraft => Set<Aircraft>();

    public DbSet<Flight> Flights => Set<Flight>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(item => item.Id);
        user.Property(item => item.Id).ValueGeneratedNever();
        user.Property(item => item.Email).HasMaxLength(256).IsRequired();
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        user.Property(item => item.Role).HasMaxLength(32).IsRequired();
        user.Property(item => item.CreatedAt).IsRequired();

        var aircraft = modelBuilder.Entity<Aircraft>();
        aircraft.ToTable("Aircraft", table =>
        {
            table.HasCheckConstraint("CK_Aircraft_SeatCapacity", "\"SeatCapacity\" >= 1 AND \"SeatCapacity\" <= 500");
        });
        aircraft.HasKey(item => item.Id);
        aircraft.Property(item => item.Id).ValueGeneratedNever();
        aircraft.Property(item => item.Registration).HasMaxLength(16).IsRequired();
        aircraft.HasIndex(item => item.Registration).IsUnique();
        aircraft.Property(item => item.Model).HasMaxLength(80).IsRequired();

        var flight = modelBuilder.Entity<Flight>();
        flight.ToTable("Flights", table =>
        {
            table.HasCheckConstraint(
                "CK_Flights_Seats",
                "\"AvailableSeats\" >= 0 AND \"TotalSeats\" >= 1 AND \"AvailableSeats\" <= \"TotalSeats\"");
        });
        flight.HasKey(item => item.Id);
        flight.Property(item => item.Id).ValueGeneratedNever();
        flight.Property(item => item.FlightNumber).HasMaxLength(8).IsRequired();
        flight.HasIndex(item => item.FlightNumber).IsUnique();
        flight.Property(item => item.DepartureAirport).HasMaxLength(3).IsRequired();
        flight.Property(item => item.ArrivalAirport).HasMaxLength(3).IsRequired();
        flight.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        flight.Property(item => item.Version).IsConcurrencyToken();
        flight.HasOne(item => item.Aircraft)
            .WithMany(item => item.Flights)
            .HasForeignKey(item => item.AircraftId)
            .OnDelete(DeleteBehavior.Restrict);

        var booking = modelBuilder.Entity<Booking>();
        booking.ToTable("Bookings");
        booking.HasKey(item => item.Id);
        booking.Property(item => item.Id).ValueGeneratedNever();
        booking.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        booking.Property(item => item.CreatedAt).IsRequired();
        booking.HasOne(item => item.Flight)
            .WithMany(item => item.Bookings)
            .HasForeignKey(item => item.FlightId)
            .OnDelete(DeleteBehavior.Restrict);
        booking.HasOne(item => item.Passenger)
            .WithMany(item => item.Bookings)
            .HasForeignKey(item => item.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);
        booking.HasIndex(item => new { item.PassengerId, item.FlightId });
    }
}
