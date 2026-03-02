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
    [EmailAddress]   // 🔥 ESTO ES CLAVE
    public string Email { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}