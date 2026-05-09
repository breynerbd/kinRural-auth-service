namespace AuthService.Application.DTOs.Auth;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}