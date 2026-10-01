using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoCodeId",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "promo_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    PercentageOff = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumPurchase = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpiresAt = table.Column<DateOnly>(type: "date", nullable: true),
                    UsageLimit = table.Column<int>(type: "integer", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_codes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_PromoCodeId",
                table: "orders",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_TenantId_BranchId_Code",
                table: "promo_codes",
                columns: new[] { "TenantId", "BranchId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_promo_codes_PromoCodeId",
                table: "orders",
                column: "PromoCodeId",
                principalTable: "promo_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: every pre-existing Order had no discount, so its Subtotal was
            // always equal to its TotalAmount — avoids every historical Order showing
            // "Subtotal: Rp0" once the UI starts reading this new column.
            migrationBuilder.Sql("UPDATE orders SET \"Subtotal\" = \"TotalAmount\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_promo_codes_PromoCodeId",
                table: "orders");

            migrationBuilder.DropTable(
                name: "promo_codes");

            migrationBuilder.DropIndex(
                name: "IX_orders_PromoCodeId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "orders");
        }
    }
}
