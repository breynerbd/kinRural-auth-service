using BCrypt.Net;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    [HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request)
{
    if (await _userRepository.ExistsByEmailAsync(request.Email))
        return BadRequest("Email already exists");


    var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
if (existingUser != null)
{
    return BadRequest(new { Message = "El nombre de usuario ya está en uso." });
}
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

    var userRole = await _roleRepository.GetByNameAsync("USER");

    await _userRepository.UpdateUserRoleAsync(user.Id, userRole.Id);

    return Ok("User registered successfully");
}

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized("Credenciales inválidas.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized("Credenciales inválidas.");

        var roles = await _userRepository.GetUserRolesAsync(user.Id);

        var token = _jwtTokenGenerator.GenerateToken(user, roles);

        return Ok(new { accessToken = token });
    }

    [Authorize]
[HttpGet("me")]
public async Task<IActionResult> Me()
{
    // Obtenemos el Id del usuario desde el token
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrEmpty(userId))
        return Unauthorized();

    // Traemos al usuario con todas sus relaciones necesarias
    var user = await _userRepository.GetByIdAsync(userId);

    if (user == null)
        return NotFound("Usuario no encontrado.");

    // Construimos la respuesta
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
        Profile = user.UserProfile != null ? new
        {
            user.UserProfile.ProfilePictureUrl,
            user.UserProfile.Bio,
            user.UserProfile.DateOfBirth
        } : null,
        Roles = user.UserRoles.Select(r => r.Role.Name).ToList()
    };

    return Ok(result);
}
}