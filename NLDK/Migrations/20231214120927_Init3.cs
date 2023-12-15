using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLDK.Migrations
{
    /// <inheritdoc />
    public partial class Init3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Channels",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Channels_FundingTransactionHash_FundingTransactionOutputIndex",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Channels_WalletId",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Channels");

            migrationBuilder.AddColumn<byte[]>(
                name: "SpendableData",
                table: "Channels",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Channels",
                table: "Channels",
                columns: new[] { "WalletId", "FundingTransactionHash", "FundingTransactionOutputIndex" });

            migrationBuilder.CreateTable(
                name: "LightningPayment",
                columns: table => new
                {
                    PaymentHash = table.Column<string>(type: "TEXT", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    Inbound = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preimage = table.Column<string>(type: "TEXT", nullable: true),
                    Secret = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Value = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightningPayment", x => new { x.WalletId, x.PaymentHash, x.Inbound });
                    table.ForeignKey(
                        name: "FK_LightningPayment_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_FundingTransactionHash_FundingTransactionOutputIndex",
                table: "Channels",
                columns: new[] { "FundingTransactionHash", "FundingTransactionOutputIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LightningPayment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Channels",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Channels_FundingTransactionHash_FundingTransactionOutputIndex",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "SpendableData",
                table: "Channels");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Channels",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Channels",
                table: "Channels",
                columns: new[] { "Id", "WalletId" });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_FundingTransactionHash_FundingTransactionOutputIndex",
                table: "Channels",
                columns: new[] { "FundingTransactionHash", "FundingTransactionOutputIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_WalletId",
                table: "Channels",
                column: "WalletId");
        }
    }
}
