using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexesForPagination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedAt_Id",
                table: "Parcels",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_EstimatedDeliveryDate_Id",
                table: "Parcels",
                columns: new[] { "EstimatedDeliveryDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_Id",
                table: "Parcels",
                columns: new[] { "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedAt_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_EstimatedDeliveryDate_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_Id",
                table: "Parcels");
        }
    }
}
