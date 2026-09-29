using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UD_Autos.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_LandedCostInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgentChargesPkr",
                table: "Shipments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AllocationMethod",
                table: "Shipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChinaProductCostPkr",
                table: "Shipments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostingExchangeRate",
                table: "Shipments",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "Shipments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalizedBy",
                table: "Shipments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HawalaDirectChargesPkr",
                table: "Shipments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsLandedCostLocked",
                table: "Shipments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LandedCostCalculatedAt",
                table: "Shipments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Shipments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDirectCostPkr",
                table: "Shipments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedDirectCostPkr",
                table: "ShipmentItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocationBasisValue",
                table: "ShipmentItems",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocationCbm",
                table: "ShipmentItems",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocationWeightKg",
                table: "ShipmentItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChinaProductCostPkr",
                table: "ShipmentItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomAllocationPercent",
                table: "ShipmentItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandedUnitCostPkr",
                table: "ShipmentItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DamagedStock",
                table: "Products",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "Products",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "InventoryBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ShipmentId = table.Column<int>(type: "int", nullable: false),
                    ShipmentItemId = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LandedUnitCostPkr = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBatches_ShipmentItems_ShipmentItemId",
                        column: x => x.ShipmentItemId,
                        principalTable: "ShipmentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBatches_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    InventoryBatchId = table.Column<int>(type: "int", nullable: true),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCostPkr = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryBatches_InventoryBatchId",
                        column: x => x.InventoryBatchId,
                        principalTable: "InventoryBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_BatchNumber",
                table: "InventoryBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ProductId_ShipmentId",
                table: "InventoryBatches",
                columns: new[] { "ProductId", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ShipmentId",
                table: "InventoryBatches",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ShipmentItemId",
                table: "InventoryBatches",
                column: "ShipmentItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryBatchId",
                table: "StockMovements",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "ProductId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId",
                table: "StockMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryBatches");

            migrationBuilder.DropColumn(
                name: "AgentChargesPkr",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "AllocationMethod",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ChinaProductCostPkr",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "CostingExchangeRate",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "FinalizedBy",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "HawalaDirectChargesPkr",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "IsLandedCostLocked",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "LandedCostCalculatedAt",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "TotalDirectCostPkr",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "AllocatedDirectCostPkr",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "AllocationBasisValue",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "AllocationCbm",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "AllocationWeightKg",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "ChinaProductCostPkr",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "CustomAllocationPercent",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "LandedUnitCostPkr",
                table: "ShipmentItems");

            migrationBuilder.DropColumn(
                name: "DamagedStock",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Products");
        }
    }
}
