using BCrypt.Net;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Interfaces;
using AuthService.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.Application.DTOs.Email;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Security.Claims;


namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;

public AuthController(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IAuthService authService,
    IHttpClientFactory httpClientFactory)
{
    _userRepository = userRepository;
    _roleRepository = roleRepository;
    _jwtTokenGenerator = jwtTokenGenerator;
    _authService = authService;
    _httpClientFactory = httpClientFactory;
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
        // ASIGNAR ROL (DINÁMICO)
        // =========================
var roleName = string.IsNullOrWhiteSpace(request.Role)
    ? RoleConstants.USER_ROL
    : request.Role.ToUpper();

if (!RoleConstants.AllowedRoles.Contains(roleName))
{
    return BadRequest(new
    {
        message = "Rol inválido."
    });
}

if (roleName == RoleConstants.MASTER_ADMIN)
{
    return BadRequest(new
    {
        message = "No puedes asignar este rol."
    });
}

var userRole = await _roleRepository.GetByNameAsync(roleName);

if (userRole == null)
{
    return BadRequest(new
    {
        message = "Rol inválido."
    });
}

        await _userRepository.UpdateUserRoleAsync(user.Id, userRole.Id);

        // =========================
        // SINCRONIZAR USER-SERVICE
        // =========================
        var httpClient = _httpClientFactory.CreateClient();

        Console.WriteLine($"ROLE ID: {userRole.Id}");
        Console.WriteLine($"USER ID: {user.Id}");

        var syncPayload = new
        {
            auth_id = user.Id,
            nombre = request.Name,
            apellido = request.Surname,
            correo = request.Email,
            dpi = request.Dpi,
            telefono = request.Telefono,
            direccion = request.Direccion,
            ingresos_mensuales = request.IngresosMensuales,
            role = userRole.Name
        };

        var json = JsonSerializer.Serialize(syncPayload);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync(
            "http://localhost:3005/kinrural/v1/internal/sync-user",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(500, new
            {
                message = "Usuario auth creado pero falló sincronización con user-service."
            });
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
                email = user.Email,
                role = userRole.Name
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
        var userId = User.FindFirst("sub")?.Value;

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
/// Actualiza el rol de un usuario.
/// </summary>



[Authorize(Roles = "MASTER_ADMIN,ADMIN")]
[HttpPut("users/{id}/role")]
public async Task<IActionResult> UpdateUserRole(
    string id,
    [FromBody] UpdateRoleRequest request)
{
// =========================
// VALIDAR USUARIO
// =========================
var user = await _userRepository.GetByIdAsync(id);

if (user == null)
{
    return NotFound(new
    {
        message = "Usuario no encontrado."
    });
}

var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

var targetRoles = user.UserRoles
    .Select(r => r.Role.Name)
    .ToList();

var targetIsMasterAdmin =
    targetRoles.Contains(RoleConstants.MASTER_ADMIN);

var targetIsAdmin =
    targetRoles.Contains(RoleConstants.ADMIN_ROL);

// =========================
// RESTRICCIONES ADMIN
// =========================
if (currentUserRole == RoleConstants.ADMIN_ROL)
{
    if (targetIsAdmin || targetIsMasterAdmin)
    {
        return StatusCode(403, new
        {
            message = "No puedes modificar administradores."
        });
    }
}

// =========================
// RESTRICCIONES MASTER_ADMIN
// =========================
if (currentUserRole == RoleConstants.MASTER_ADMIN)
{
    if (targetIsMasterAdmin)
    {
        return StatusCode(403, new
        {
            message = "No puedes modificar otro MASTER_ADMIN."
        });
    }
}

    // =========================
    // VALIDAR ROL
    // =========================
    var role = await _roleRepository.GetByNameAsync(request.Role);

    if (role == null)
    {
        return NotFound(new
        {
            message = "Rol no encontrado."
        });
    }

    // =========================
    // ACTUALIZAR ROL
    // =========================
    await _userRepository.UpdateUserRoleAsync(id, role.Id);

    return Ok(new
    {
        message = "Rol actualizado correctamente."
    });
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