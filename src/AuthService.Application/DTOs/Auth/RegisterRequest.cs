namespace AuthService.Application.DTOs.Auth;

using System.ComponentModel.DataAnnotations;

public class RegisterRequest
{
    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Surname { get; set; }

    [Required]
    [MaxLength(50)]
    public required  string Username { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [MinLength(8)]
    public required string Password { get; set; }

    [Required]
    [StringLength(13, MinimumLength = 13)]
    public required string Dpi { get; set; }

    [Required]
    public required string Telefono { get; set; }

    [Required]
    public required string Direccion { get; set; }

    [Required]
    public required decimal IngresosMensuales { get; set; }

    // NUEVO
    public required string Role { get; set; } = "USER";
}