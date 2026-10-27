using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace SmartCampus.Bookings.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    space_type_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    space_building_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    space_access_mode_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    space_capacity_snapshot = table.Column<int>(type: "integer", nullable: false),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    period = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    seats_requested = table.Column<int>(type: "integer", nullable: false),
                    is_group = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_period_valid", "NOT isempty(period) AND lower(period) < upper(period)");
                    table.CheckConstraint("ck_bookings_seats_positive", "seats_requested > 0");
                });

            migrationBuilder.CreateTable(
                name: "admin_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    handled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    handling_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_admin_alerts_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "closing_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_used = table.Column<bool>(type: "boolean", nullable: false),
                    attendees_count = table.Column<int>(type: "integer", nullable: false),
                    issues_reported = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_closing_comments", x => x.id);
                    table.CheckConstraint("ck_closing_comments_attendees_non_negative", "attendees_count >= 0");
                    table.CheckConstraint("ck_closing_comments_comment_length", "char_length(comment) BETWEEN 10 AND 500");
                    table.ForeignKey(
                        name: "fk_closing_comments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    handled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_requests_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.CheckConstraint("ck_invitations_period_valid", "NOT isempty(period) AND lower(period) < upper(period)");
                    table.ForeignKey(
                        name: "fk_invitations_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_emails", x => x.id);
                    table.ForeignKey(
                        name: "fk_outbox_emails_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_alerts_booking_id",
                table: "admin_alerts",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_alerts_status_created_at",
                table: "admin_alerts",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_organizer",
                table: "bookings",
                column: "organizer_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_space_period",
                table: "bookings",
                columns: new[] { "space_id", "period" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_bookings_reference_code",
                table: "bookings",
                column: "reference_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_closing_comments_author",
                table: "closing_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ux_closing_comments_booking",
                table: "closing_comments",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_requests_booking",
                table: "equipment_requests",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_requests_status",
                table: "equipment_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email",
                table: "invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_status_expires_at",
                table: "invitations",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_user",
                table: "invitations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_invitations_booking_email",
                table: "invitations",
                columns: new[] { "booking_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_emails_booking_id",
                table: "outbox_emails",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_emails_status_created_at",
                table: "outbox_emails",
                columns: new[] { "status", "created_at" });

            migrationBuilder.Sql(
                "ALTER TABLE bookings ADD CONSTRAINT ex_bookings_dedicated_space_no_overlap " +
                "EXCLUDE USING gist (space_id WITH =, period WITH &&) " +
                "WHERE (status = 'Confirmed' AND space_access_mode_snapshot = 'Dedicated');");

            migrationBuilder.Sql(
                "ALTER TABLE invitations ADD CONSTRAINT ex_invitations_person_no_overlap " +
                "EXCLUDE USING gist (user_id WITH =, period WITH &&) " +
                "WHERE (status = 'Accepted' AND user_id IS NOT NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_alerts");

            migrationBuilder.DropTable(
                name: "closing_comments");

            migrationBuilder.DropTable(
                name: "equipment_requests");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "outbox_emails");

            migrationBuilder.DropTable(
                name: "bookings");
        }
    }
}
