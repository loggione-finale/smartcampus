using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;

namespace SmartCampus.Users.Api.Data.Seed;

public class UsersDbSeeder(
    UsersDbContext dbContext,
    IPasswordHasher<UserProfile> passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Seeding is enabled in Development and Testing by default, or explicitly via configuration
        var isSeedingEnabled = environment.IsDevelopment()
            || environment.IsEnvironment("Testing")
            || configuration.GetValue<bool>("Seed:Enabled", false);

        if (!isSeedingEnabled)
        {
            return;
        }

        if (await dbContext.UserProfiles.AnyAsync(cancellationToken))
        {
            return;
        }

        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        var adminPassword = configuration["Seed:AdminPassword"]
            ?? (isDevOrTest ? "AdminPass123" : throw new InvalidOperationException("Missing required configuration 'Seed:AdminPassword' for seeding in non-development environment."));

        var defaultUserPassword = configuration["Seed:DefaultUserPassword"]
            ?? (isDevOrTest ? "Password123" : throw new InvalidOperationException("Missing required configuration 'Seed:DefaultUserPassword' for seeding in non-development environment."));

        var users = new List<UserProfile>();

        // 1. Pre-loaded Admin account
        var admin = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000001"),
            Matricola = "0000000001",
            FirstName = "Admin",
            LastName = "Campus",
            DisplayName = "Admin Campus",
            Email = "admin@smartcampus.test",
            PhoneNumber = "+390801234567",
            Role = UserRole.Admin,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
        users.Add(admin);

        // 2. Canonical Synthetic Student accounts (aligned with data/synthetic and Bookings seed)
        var giulia = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000002"),
            Matricola = "1000000001",
            FirstName = "Giulia",
            LastName = "Rossi",
            DisplayName = "Giulia Rossi",
            Email = "giulia.rossi@smartcampus.test",
            PhoneNumber = "+393331122334",
            Role = UserRole.User,
            IsActive = true
        };
        giulia.PasswordHash = passwordHasher.HashPassword(giulia, defaultUserPassword);
        users.Add(giulia);

        var marco = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000003"),
            Matricola = "1000000002",
            FirstName = "Marco",
            LastName = "Bianchi",
            DisplayName = "Marco Bianchi",
            Email = "marco.bianchi@smartcampus.test",
            PhoneNumber = "+393332233445",
            Role = UserRole.User,
            IsActive = true
        };
        marco.PasswordHash = passwordHasher.HashPassword(marco, defaultUserPassword);
        users.Add(marco);

        var sara = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000004"),
            Matricola = "1000000003",
            FirstName = "Sara",
            LastName = "Conti",
            DisplayName = "Sara Conti",
            Email = "sara.conti@smartcampus.test",
            PhoneNumber = "+393333344556",
            Role = UserRole.User,
            IsActive = true
        };
        sara.PasswordHash = passwordHasher.HashPassword(sara, defaultUserPassword);
        users.Add(sara);

        var luca = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000005"),
            Matricola = "1000000004",
            FirstName = "Luca",
            LastName = "Ferrari",
            DisplayName = "Luca Ferrari",
            Email = "luca.ferrari@smartcampus.test",
            PhoneNumber = "+393334455667",
            Role = UserRole.User,
            IsActive = true
        };
        luca.PasswordHash = passwordHasher.HashPassword(luca, defaultUserPassword);
        users.Add(luca);

        var elena = new UserProfile
        {
            Id = Guid.Parse("b7e1c2d3-0000-4000-8000-000000000006"),
            Matricola = "1000000005",
            FirstName = "Elena",
            LastName = "Greco",
            DisplayName = "Elena Greco",
            Email = "elena.greco@smartcampus.test",
            PhoneNumber = "+393335566778",
            Role = UserRole.User,
            IsActive = true
        };
        elena.PasswordHash = passwordHasher.HashPassword(elena, defaultUserPassword);
        users.Add(elena);

        dbContext.UserProfiles.AddRange(users);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
