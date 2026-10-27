using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Bookings.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDateAndTimeBand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "booking_date",
                table: "bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_band",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE bookings SET " +
                "booking_date = (lower(period) AT TIME ZONE 'Europe/Rome')::date, " +
                "time_band = CASE " +
                "WHEN extract(hour FROM (lower(period) AT TIME ZONE 'Europe/Rome')) < 14 THEN 'Morning' " +
                "WHEN extract(hour FROM (lower(period) AT TIME ZONE 'Europe/Rome')) < 19 THEN 'Afternoon' " +
                "ELSE 'Evening' END;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "booking_date",
                table: "bookings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "time_band",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_bookings_organizer_date_band",
                table: "bookings",
                columns: new[] { "organizer_id", "booking_date", "time_band" },
                unique: true,
                filter: "status = 'Confirmed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bookings_organizer_date_band",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "booking_date",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "time_band",
                table: "bookings");
        }
    }
}
