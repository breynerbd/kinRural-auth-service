using BCrypt.Net;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.Application.DTOs.Email;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuthService _authService;

    public AuthController(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IAuthService authService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _authService = authService;
    }

    /// <summary>
    /// Registra un nuevo usuario.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            // =========================
            // VALIDAR EMAIL
            // =========================
            if (await _userRepository.ExistsByEmailAsync(request.Email))
            {
                return BadRequest(new
                {
                    message = "El correo electrónico ya está en uso."
                });
            }

            // =========================
            // VALIDAR USERNAME
            // =========================
            var existingUser = await _userRepository.GetByUsernameAsync(request.Username);

            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message = "El nombre de usuario ya está en uso."
                });
            }

            // =========================
            // CREAR USUARIO AUTH
            // =========================
            var user = new User
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = request.Name,
                Surname = request.Surname,
                Username = request.Username,
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user);

            // =========================
            // ASIGNAR ROL
            // =========================
            var userRole = await _roleRepository.GetByNameAsync("USER");

            await _userRepository.UpdateUserRoleAsync(user.Id, userRole.Id);

            // =========================
            // SINCRONIZAR USER-SERVICE
            // =========================
            try
            {
                var httpClient = new HttpClient();

                var payload = new
                {
                    auth_id = user.Id,
                    nombre = user.Name,
                    apellido = user.Surname,
                    correo = user.Email,
                    dpi = (string?)null,
                    telefono = "PENDIENTE",
                    direccion = "PENDIENTE",
                    ingresos_mensuales = 0,
                    role_id = 2
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.PostAsync(
                    "http://host.docker.internal:3005/kinrural/v1/internal/sync-user",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ Error sincronizando user-service");
                }
            }
            catch (Exception syncError)
            {
                Console.WriteLine($"❌ Sync Error: {syncError.Message}");
            }

            // =========================
            // RESPUESTA
            // =========================
            return Ok(new
            {
                message = "User registered successfully",
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Inicia sesión.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        User? user;

        // =========================
        // LOGIN POR EMAIL O USERNAME
        // =========================
        if (request.Identifier.Contains("@"))
        {
            user = await _userRepository.GetByEmailAsync(request.Identifier);
        }
        else
        {
            user = await _userRepository.GetByUsernameAsync(request.Identifier);
        }

        if (user == null)
        {
            return Unauthorized("Credenciales inválidas.");
        }

        // =========================
        // VALIDAR PASSWORD
        // =========================
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            return Unauthorized("Credenciales inválidas.");
        }

        // =========================
        // GENERAR TOKEN
        // =========================
        var roles = await _userRepository.GetUserRolesAsync(user.Id);

        var token = _jwtTokenGenerator.GenerateToken(user, roles);

        return Ok(new
        {
            accessToken = token
        });
    }

    /// <summary>
    /// Perfil del usuario autenticado.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst("id")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return NotFound("Usuario no encontrado.");
        }

        var result = new
        {
            user.Id,
            user.Name,
            user.Surname,
            user.Username,
            user.Email,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,

            EmailVerified = user.UserEmail?.EmailVerified ?? false,

            Profile = user.UserProfile != null
                ? new
                {
                    user.UserProfile.ProfilePictureUrl,
                    user.UserProfile.Bio,
                    user.UserProfile.DateOfBirth
                }
                : null,

            Roles = user.UserRoles.Select(r => r.Role.Name).ToList()
        };

        return Ok(result);
    }

    /// <summary>
    /// Solicita recuperación de contraseña.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ForgotPassword(
        [FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);

        if (!result.Success)
        {
            return StatusCode(503, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Restablece contraseña.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ResetPassword(
        [FromBody] ResetPasswordDto resetPasswordDto)
    {
        var result = await _authService.ResetPasswordAsync(resetPasswordDto);

        return Ok(result);
    }

    /// <summary>
    /// Reenvía verificación.
    /// </summary>
    [HttpPost("resend-verification")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ResendVerification(
        [FromBody] ResendVerificationDto resendDto)
    {
        var result = await _authService.ResendVerificationEmailAsync(resendDto);

        if (!result.Success)
        {
            if (result.Message.Contains("no encontrado",
                StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }

            if (result.Message.Contains("ya ha sido verificado",
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(result);
            }

            return StatusCode(503, result);
        }

        return Ok(result);
    }
}