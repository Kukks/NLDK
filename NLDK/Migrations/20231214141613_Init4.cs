using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLDK.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LightningPayment_Wallets_WalletId",
                table: "LightningPayment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LightningPayment",
                table: "LightningPayment");

            migrationBuilder.RenameTable(
                name: "LightningPayment",
                newName: "LightningPayments");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "LightningPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments",
                columns: new[] { "WalletId", "PaymentHash", "Inbound" });

            migrationBuilder.AddForeignKey(
                name: "FK_LightningPayments_Wallets_WalletId",
                table: "LightningPayments",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LightningPayments_Wallets_WalletId",
                table: "LightningPayments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LightningPayments",
                table: "LightningPayments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LightningPayments");

            migrationBuilder.RenameTable(
                name: "LightningPayments",
                newName: "LightningPayment");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LightningPayment",
                table: "LightningPayment",
                columns: new[] { "WalletId", "PaymentHash", "Inbound" });

            migrationBuilder.AddForeignKey(
                name: "FK_LightningPayment_Wallets_WalletId",
                table: "LightningPayment",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
