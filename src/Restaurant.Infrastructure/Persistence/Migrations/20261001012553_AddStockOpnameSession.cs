using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockOpnameSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpnameSessionId",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityBefore",
                table: "stock_movements",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_opname_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_opname_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_OpnameSessionId",
                table: "stock_movements",
                column: "OpnameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_opname_sessions_TenantId_BranchId",
                table: "stock_opname_sessions",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_stock_opname_sessions_OpnameSessionId",
                table: "stock_movements",
                column: "OpnameSessionId",
                principalTable: "stock_opname_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_stock_opname_sessions_OpnameSessionId",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_opname_sessions");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_OpnameSessionId",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "OpnameSessionId",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "QuantityBefore",
                table: "stock_movements");
        }
    }
}
