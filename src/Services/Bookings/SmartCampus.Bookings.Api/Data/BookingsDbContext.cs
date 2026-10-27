using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data;

public class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<EquipmentRequest> EquipmentRequests => Set<EquipmentRequest>();
    public DbSet<ClosingComment> ClosingComments => Set<ClosingComment>();
    public DbSet<AdminAlert> AdminAlerts => Set<AdminAlert>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings", table =>
            {
                table.HasCheckConstraint("ck_bookings_period_valid", "NOT isempty(period) AND lower(period) < upper(period)");
                table.HasCheckConstraint("ck_bookings_seats_positive", "seats_requested > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferenceCode).HasMaxLength(32);
            entity.Property(x => x.SpaceNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.SpaceTypeSnapshot).HasMaxLength(50);
            entity.Property(x => x.SpaceBuildingSnapshot).HasMaxLength(200);
            entity.Property(x => x.OrganizerNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.Period).HasColumnType("tstzrange");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.SpaceAccessModeSnapshot).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.TimeBand).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.CancellationReason).HasMaxLength(500);

            entity.HasIndex(x => x.ReferenceCode).IsUnique().HasDatabaseName("ux_bookings_reference_code");
            entity.HasIndex(x => x.OrganizerId).HasDatabaseName("ix_bookings_organizer");
            entity.HasIndex(x => new { x.OrganizerId, x.BookingDate, x.TimeBand })
                .IsUnique()
                .HasFilter("status = 'Confirmed'")
                .HasDatabaseName("ux_bookings_organizer_date_band");
            entity.HasIndex(x => new { x.SpaceId, x.Period }).HasMethod("gist").HasDatabaseName("ix_bookings_space_period");
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations", table =>
            {
                table.HasCheckConstraint("ck_invitations_period_valid", "NOT isempty(period) AND lower(period) < upper(period)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.DisplayNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Period).HasColumnType("tstzrange");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Booking).WithMany(x => x.Invitations).HasForeignKey(x => x.BookingId);
            entity.HasIndex(x => new { x.BookingId, x.Email }).IsUnique().HasDatabaseName("ux_invitations_booking_email");
            entity.HasIndex(x => x.UserId).HasDatabaseName("ix_invitations_user");
            entity.HasIndex(x => x.Email).HasDatabaseName("ix_invitations_email");
            entity.HasIndex(x => new { x.Status, x.ExpiresAt }).HasDatabaseName("ix_invitations_status_expires_at");
        });

        modelBuilder.Entity<EquipmentRequest>(entity =>
        {
            entity.ToTable("equipment_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EquipmentType).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Booking).WithMany(x => x.EquipmentRequests).HasForeignKey(x => x.BookingId);
            entity.HasIndex(x => x.BookingId).HasDatabaseName("ix_equipment_requests_booking");
            entity.HasIndex(x => x.Status).HasDatabaseName("ix_equipment_requests_status");
        });

        modelBuilder.Entity<ClosingComment>(entity =>
        {
            entity.ToTable("closing_comments", table =>
            {
                table.HasCheckConstraint("ck_closing_comments_attendees_non_negative", "attendees_count >= 0");
                table.HasCheckConstraint("ck_closing_comments_comment_length", "char_length(comment) BETWEEN 10 AND 500");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Booking).WithOne(x => x.ClosingComment).HasForeignKey<ClosingComment>(x => x.BookingId);
            entity.HasIndex(x => x.BookingId).IsUnique().HasDatabaseName("ux_closing_comments_booking");
            entity.HasIndex(x => x.AuthorId).HasDatabaseName("ix_closing_comments_author");
        });

        modelBuilder.Entity<AdminAlert>(entity =>
        {
            entity.ToTable("admin_alerts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.HandlingNote).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId);
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_admin_alerts_status_created_at");
        });

        modelBuilder.Entity<OutboxEmail>(entity =>
        {
            entity.ToTable("outbox_emails");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToAddress).HasMaxLength(254);
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.Kind).HasMaxLength(50);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId);
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_outbox_emails_status_created_at");
        });
    }
}
