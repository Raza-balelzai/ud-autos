using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UD_Autos.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_ProductSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductKind",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InvoiceSetComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceItemId = table.Column<int>(type: "int", nullable: false),
                    ComponentProductId = table.Column<int>(type: "int", nullable: false),
                    RequiredQuantityPerSet = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityConsumed = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCogsPkr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineCogsPkr = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSetComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSetComponents_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceSetComponents_Products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductSetComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SetProductId = table.Column<int>(type: "int", nullable: false),
                    ComponentProductId = table.Column<int>(type: "int", nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSetComponents", x => x.Id);
                    table.CheckConstraint("CK_ProductSetComponents_NotSelf", "[SetProductId] <> [ComponentProductId]");
                    table.CheckConstraint("CK_ProductSetComponents_RequiredQuantity", "[RequiredQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_ProductSetComponents_Products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSetComponents_Products_SetProductId",
                        column: x => x.SetProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSetComponentBatchAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceSetComponentId = table.Column<int>(type: "int", nullable: false),
                    InventoryBatchId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCostPkr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineCostPkr = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSetComponentBatchAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSetComponentBatchAllocations_InventoryBatches_InventoryBatchId",
                        column: x => x.InventoryBatchId,
                        principalTable: "InventoryBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceSetComponentBatchAllocations_InvoiceSetComponents_InvoiceSetComponentId",
                        column: x => x.InvoiceSetComponentId,
                        principalTable: "InvoiceSetComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductKind",
                table: "Products",
                column: "ProductKind");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSetComponentBatchAllocations_InventoryBatchId",
                table: "InvoiceSetComponentBatchAllocations",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSetComponentBatchAllocations_InvoiceSetComponentId_InventoryBatchId",
                table: "InvoiceSetComponentBatchAllocations",
                columns: new[] { "InvoiceSetComponentId", "InventoryBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSetComponents_ComponentProductId",
                table: "InvoiceSetComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSetComponents_InvoiceItemId_ComponentProductId",
                table: "InvoiceSetComponents",
                columns: new[] { "InvoiceItemId", "ComponentProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSetComponents_ComponentProductId",
                table: "ProductSetComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSetComponents_SetProductId_ComponentProductId",
                table: "ProductSetComponents",
                columns: new[] { "SetProductId", "ComponentProductId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceSetComponentBatchAllocations");

            migrationBuilder.DropTable(
                name: "ProductSetComponents");

            migrationBuilder.DropTable(
                name: "InvoiceSetComponents");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductKind",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductKind",
                table: "Products");
        }
    }
}
