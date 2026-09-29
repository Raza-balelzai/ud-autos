using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UD_Autos.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_HardeningIdempotencyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Invoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedBy",
                table: "Invoices",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingExpenses_Account",
                table: "OperatingExpenses",
                column: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IdempotencyKey",
                table: "Invoices",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ProductId_ReceivedAt",
                table: "InventoryBatches",
                columns: new[] { "ProductId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Account",
                table: "CustomerPayments",
                column: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_PaymentDate",
                table: "CustomerPayments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashBankMovements_MovementType_MovementDate",
                table: "CashBankMovements",
                columns: new[] { "MovementType", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_TimestampUtc",
                table: "AuditLogs",
                columns: new[] { "Action", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperatingExpenses_Account",
                table: "OperatingExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_IdempotencyKey",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBatches_ProductId_ReceivedAt",
                table: "InventoryBatches");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_Account",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_PaymentDate",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CashBankMovements_MovementType_MovementDate",
                table: "CashBankMovements");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action_TimestampUtc",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Invoices");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
