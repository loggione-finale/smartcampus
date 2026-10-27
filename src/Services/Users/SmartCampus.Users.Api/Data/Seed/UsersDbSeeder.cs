using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;

namespace SmartCampus.Users.Api.Data.Seed;

public class UsersDbSeeder(UsersDbContext dbContext, IPasswordHasher<UserProfile> passwordHasher)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.UserProfiles.AnyAsync(cancellationToken))
        {
            return;
        }

        var users = new List<UserProfile>();

        // 1. Pre-loaded Admin account
        var admin = new UserProfile
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Matricola = "00000001",
            FirstName = "Admin",
            LastName = "Campus",
            DisplayName = "Admin Campus",
            Email = "admin@smartcampus.test",
            PhoneNumber = "+390801234567",
            Role = UserRole.Admin,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "AdminPass123");
        users.Add(admin);

        // 2. Synthetic Student accounts
        var student1 = new UserProfile
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            Matricola = "10000001",
            FirstName = "Mario",
            LastName = "Rossi",
            DisplayName = "Mario Rossi",
            Email = "mario.rossi@smartcampus.test",
            PhoneNumber = "+393331122334",
            Role = UserRole.User,
            IsActive = true
        };
        student1.PasswordHash = passwordHasher.HashPassword(student1, "Password123");
        users.Add(student1);

        var student2 = new UserProfile
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
            Matricola = "10000002",
            FirstName = "Laura",
            LastName = "Bianchi",
            DisplayName = "Laura Bianchi",
            Email = "laura.bianchi@smartcampus.test",
            PhoneNumber = "+393335566778",
            Role = UserRole.User,
            IsActive = true
        };
        student2.PasswordHash = passwordHasher.HashPassword(student2, "Password123");
        users.Add(student2);

        var student3 = new UserProfile
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000013"),
            Matricola = "10000003",
            FirstName = "Giulia",
            LastName = "Verdi",
            DisplayName = "Giulia Verdi",
            Email = "giulia.verdi@smartcampus.test",
            PhoneNumber = "+393339900112",
            Role = UserRole.User,
            IsActive = true
        };
        student3.PasswordHash = passwordHasher.HashPassword(student3, "Password123");
        users.Add(student3);

        dbContext.UserProfiles.AddRange(users);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
