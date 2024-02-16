using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLDK.Migrations
{
    /// <inheritdoc />
    public partial class RemovePaymentIdAsKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentId",
                table: "LightningPayments",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments",
                columns: new[] { "WalletId", "PaymentHash", "Inbound" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentId",
                table: "LightningPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments",
                columns: new[] { "WalletId", "PaymentHash", "Inbound", "PaymentId" });
        }
    }
}
