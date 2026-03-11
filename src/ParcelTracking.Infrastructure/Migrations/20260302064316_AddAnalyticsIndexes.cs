using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_EventType_Timestamp",
                table: "TrackingEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedAt_Status",
                table: "Parcels",
                columns: new[] { "CreatedAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_EventType_Timestamp",
                table: "TrackingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedAt_Status",
                table: "Parcels");
        }
    }
}
