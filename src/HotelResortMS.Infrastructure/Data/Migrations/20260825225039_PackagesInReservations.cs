using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelResortMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PackagesInReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagePrice",
                table: "Reservations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PackageId",
                table: "Reservations",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Packages_PackageId",
                table: "Reservations",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Packages_PackageId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_PackageId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PackageId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PackagePrice",
                table: "Reservations");
        }
    }
}
