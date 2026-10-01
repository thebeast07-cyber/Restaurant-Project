using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "recipe_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_items_ProductVariantId",
                table: "recipe_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ProductVariantId",
                table: "order_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_product_variants_ProductVariantId",
                table: "order_items",
                column: "ProductVariantId",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_items_product_variants_ProductVariantId",
                table: "recipe_items",
                column: "ProductVariantId",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_product_variants_ProductVariantId",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_items_product_variants_ProductVariantId",
                table: "recipe_items");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropIndex(
                name: "IX_recipe_items_ProductVariantId",
                table: "recipe_items");

            migrationBuilder.DropIndex(
                name: "IX_order_items_ProductVariantId",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "recipe_items");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "order_items");
        }
    }
}
