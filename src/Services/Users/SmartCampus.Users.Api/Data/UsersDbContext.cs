using Microsoft.EntityFrameworkCore;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;

namespace SmartCampus.Users.Api.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Matricola)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(x => x.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.LastName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(254);

            entity.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(15);

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue(UserRole.User);

            entity.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("now()");

            entity.HasIndex(x => x.Matricola)
                .IsUnique()
                .HasDatabaseName("ux_user_profiles_matricola");

            entity.HasIndex(x => x.Email)
                .IsUnique()
                .HasDatabaseName("ux_user_profiles_email");

            entity.HasIndex(x => x.DisplayName)
                .HasDatabaseName("ix_user_profiles_display_name");
        });
    }
}
