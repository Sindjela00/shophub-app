using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShopSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Availability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WalletAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DatabaseKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopSites_UserId",
                table: "ShopSites",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopSites");
        }
    }
}
