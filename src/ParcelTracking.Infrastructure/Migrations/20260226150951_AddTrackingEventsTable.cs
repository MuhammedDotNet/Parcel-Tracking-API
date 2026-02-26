using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_ParcelId",
                table: "TrackingEvents");

            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_Timestamp",
                table: "TrackingEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "Addresses",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "Addresses",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_ParcelId_Timestamp",
                table: "TrackingEvents",
                columns: new[] { "ParcelId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_ParcelId_Timestamp",
                table: "TrackingEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "Addresses",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "Addresses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_ParcelId",
                table: "TrackingEvents",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_Timestamp",
                table: "TrackingEvents",
                column: "Timestamp");
        }
    }
}
