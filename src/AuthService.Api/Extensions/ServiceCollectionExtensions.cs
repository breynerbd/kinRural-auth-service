using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Constants;
using AuthService.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // INICIALIZANDO EL CONEXIÓN A LA BASE DE DATOS
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .UseSnakeCaseNamingConvention());

        // INICIALIZANDO EL SERVICIO DE EMAIL
        services.AddScoped<IEmailService, EmailService>();

        services.AddHealthChecks();

        return services;
    }
}