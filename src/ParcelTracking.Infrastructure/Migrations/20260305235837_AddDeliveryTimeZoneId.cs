using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryTimeZoneId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryTimeZoneId",
                table: "Parcels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryTimeZoneId",
                table: "Parcels");
        }
    }
}
