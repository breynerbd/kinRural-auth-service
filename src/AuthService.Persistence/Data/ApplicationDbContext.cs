using AuthService.Domain.Entitis;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Persistence.Data;

public class ApplicationDbContext : DbContext 
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
        : base(options) 
    {
    }
    
    public DbSet<User> Users { get; set; } 
    public DbSet<UserProfile> UserProfiles { get; set; } 
    public DbSet<Role> Roles { get; set; } 
    public DbSet<UserRole> UserRoles { get; set; } 
    public DbSet<UserEmail> UserEmails { get; set; } 
    public DbSet<UserPasswordReset> UserPasswordResets { get; set; } 

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Esto asegura que EF Core use correctamente PostgreSQL
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator,
            Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.NpgsqlMigrationsSqlGenerator>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // --- Convertir camelCase a snake_case ---
    foreach (var entity in modelBuilder.Model.GetEntityTypes())
    {
        var tableName = entity.GetTableName();
        if (!string.IsNullOrEmpty(tableName))
            entity.SetTableName(ToSnakeCase(tableName));

        foreach (var property in entity.GetProperties())
        {
            var columnName = property.GetColumnName();
            if (!string.IsNullOrEmpty(columnName))
                property.SetColumnName(ToSnakeCase(columnName));
        }
    }

    // --- Configuración explícita de tablas ---
    modelBuilder.Entity<Role>().ToTable("roles");
    modelBuilder.Entity<User>().ToTable("users");
    modelBuilder.Entity<UserProfile>().ToTable("user_profiles");
    modelBuilder.Entity<UserRole>().ToTable("user_roles");
    modelBuilder.Entity<UserEmail>().ToTable("user_emails");
    modelBuilder.Entity<UserPasswordReset>().ToTable("user_password_resets");

    // --- Relaciones y restricciones ---
    modelBuilder.Entity<User>(entity =>
    {
        entity.HasIndex(e => e.Id).IsUnique();
        entity.HasIndex(e => e.Email).IsUnique();
        entity.HasIndex(e => e.Username).IsUnique();

        entity.HasOne(e => e.UserProfile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.UserEmail)
            .WithOne(ue => ue.User)
            .HasForeignKey<UserEmail>(ue => ue.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.UserPasswordReset)
            .WithOne(upr => upr.User)
            .HasForeignKey<UserPasswordReset>(upr => upr.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    });

    modelBuilder.Entity<UserRole>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
    });

    modelBuilder.Entity<Role>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Name).IsUnique();
    });
}

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return string.Concat(input.Select((x, i) =>
            i > 0 && char.IsUpper(x) ? "_" + x.ToString().ToLower() : x.ToString().ToLower()
        ));
    }
}