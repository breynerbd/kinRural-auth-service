using AuthService.Application.DTOs.Email;
using AuthService.Application.Interfaces;

namespace AuthService.Application.Services;

public class AuthServiceImpl : IAuthService
{
    public Task<EmailResponseDto> VerifyEmailAsync(VerifyEmailDto verifyEmailDto)
    {
        return Task.FromResult(new EmailResponseDto
        {
            Success = true,
            Message = "Email verificado"
        });
    }

    public Task<EmailResponseDto> ResendVerificationEmailAsync(ResendVerificationDto resendDto)
    {
        return Task.FromResult(new EmailResponseDto
        {
            Success = true,
            Message = "Correo reenviado"
        });
    }

    public Task<EmailResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        return Task.FromResult(new EmailResponseDto
        {
            Success = true,
            Message = "Correo de recuperación enviado"
        });
    }

    public Task<EmailResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        return Task.FromResult(new EmailResponseDto
        {
            Success = true,
            Message = "Contraseña actualizada"
        });
    }
}