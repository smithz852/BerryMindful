using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BerryMindful.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItemStatusChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAt",
                table: "PantryItems",
                type: "datetime(6)",
                nullable: true);

            // Best-available approximation for rows resolved before this column existed.
            migrationBuilder.Sql(
                "UPDATE PantryItems SET StatusChangedAt = UpdatedAt WHERE Status IN ('Used','Tossed');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusChangedAt",
                table: "PantryItems");
        }
    }
}
