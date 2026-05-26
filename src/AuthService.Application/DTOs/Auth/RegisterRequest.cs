namespace AuthService.Application.DTOs.Auth;

using System.ComponentModel.DataAnnotations;

public class RegisterRequest
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public string Surname { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }

    [Required]
    [StringLength(13, MinimumLength = 13)]
    public string Dpi { get; set; }

    [Required]
    public string Telefono { get; set; }

    [Required]
    public string Direccion { get; set; }

    [Required]
    public decimal IngresosMensuales { get; set; }

    // NUEVO
    public string Role { get; set; } = "USER";
}