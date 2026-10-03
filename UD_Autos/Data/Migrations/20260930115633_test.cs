using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UD_Autos.Data.Migrations
{
    /// <inheritdoc />
    public partial class test : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_OperatingExpenses_ExpenseNumber",
                table: "OperatingExpenses");

            migrationBuilder.DropIndex(
                name: "IX_HawalaPayments_PaymentNumber",
                table: "HawalaPayments");

            migrationBuilder.AlterColumn<string>(
                name: "ShipmentNumber",
                table: "Shipments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "PoNumber",
                table: "PurchaseOrders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseNumber",
                table: "OperatingExpenses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentNumber",
                table: "HawalaPayments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments",
                column: "ShipmentNumber",
                unique: true,
                filter: "[ShipmentNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders",
                column: "PoNumber",
                unique: true,
                filter: "[PoNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingExpenses_ExpenseNumber",
                table: "OperatingExpenses",
                column: "ExpenseNumber",
                unique: true,
                filter: "[ExpenseNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HawalaPayments_PaymentNumber",
                table: "HawalaPayments",
                column: "PaymentNumber",
                unique: true,
                filter: "[PaymentNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_OperatingExpenses_ExpenseNumber",
                table: "OperatingExpenses");

            migrationBuilder.DropIndex(
                name: "IX_HawalaPayments_PaymentNumber",
                table: "HawalaPayments");

            migrationBuilder.AlterColumn<string>(
                name: "ShipmentNumber",
                table: "Shipments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PoNumber",
                table: "PurchaseOrders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseNumber",
                table: "OperatingExpenses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentNumber",
                table: "HawalaPayments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments",
                column: "ShipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders",
                column: "PoNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingExpenses_ExpenseNumber",
                table: "OperatingExpenses",
                column: "ExpenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HawalaPayments_PaymentNumber",
                table: "HawalaPayments",
                column: "PaymentNumber",
                unique: true);
        }
    }
}
