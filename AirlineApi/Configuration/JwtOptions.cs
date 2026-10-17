using System.ComponentModel.DataAnnotations;

namespace AirlineApi.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required]
    public string SigningKey { get; set; } = "";

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;
}
