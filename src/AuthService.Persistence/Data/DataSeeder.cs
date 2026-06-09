using AuthService.Domain.Entitis;
using AuthService.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace AuthService.Persistence.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1️⃣ Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var masterAdminRole = new Role
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = RoleConstants.MASTER_ADMIN,
                Description = "Master administrator role"
            };

            var adminRole = new Role
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = RoleConstants.ADMIN_ROL,
                Description = "Administrator role"
            };

            var userRole = new Role
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = RoleConstants.USER_ROL,
                Description = "Standard user role"
            };

            await context.Roles.AddRangeAsync(
                masterAdminRole,
                adminRole,
                userRole
            );

            await context.SaveChangesAsync();
        }

        // 2️⃣ Seed Master Admin User
        if (!await context.Users.AnyAsync())
        {
            var masterAdminRole = await context.Roles
                .FirstOrDefaultAsync(r =>
                    r.Name == RoleConstants.MASTER_ADMIN);

            if (masterAdminRole == null) return;

            var userId = Guid.NewGuid().ToString("N")[..16];

            var masterAdminUser = new User
            {
                Id = userId,
                Name = "master",
                Surname = "admin",
                Username = "masteradmin",
                Email = "master@kinrural.local",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123!"),
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                UserProfile = new UserProfile
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    UserId = userId
                },

                UserEmail = new UserEmail
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    UserId = userId,
                    EmailVerified = true
                },

                UserRoles = new List<UserRole>
                {
                    new UserRole
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        UserId = userId,
                        RoleId = masterAdminRole.Id
                    }
                }
            };

            await context.Users.AddAsync(masterAdminUser);
            await context.SaveChangesAsync();
        }
    }
}