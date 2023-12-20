using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLDK.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scripts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scripts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    BlockHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AliasWalletName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Mnemonic = table.Column<string>(type: "TEXT", nullable: false),
                    DerivationPath = table.Column<string>(type: "TEXT", nullable: false),
                    LastDerivationIndex = table.Column<uint>(type: "INTEGER", nullable: false),
                    CreationBlockHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coins",
                columns: table => new
                {
                    FundingTransactionHash = table.Column<string>(type: "TEXT", nullable: false),
                    FundingTransactionOutputIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ScriptId = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    SpendingTransactionHash = table.Column<string>(type: "TEXT", nullable: true),
                    SpendingTransactionInputIndex = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coins", x => new { x.FundingTransactionHash, x.FundingTransactionOutputIndex });
                    table.ForeignKey(
                        name: "FK_Coins_Scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalTable: "Scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionScripts",
                columns: table => new
                {
                    TransactionHash = table.Column<string>(type: "TEXT", nullable: false),
                    ScriptId = table.Column<string>(type: "TEXT", nullable: false),
                    Spent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionScripts", x => new { x.ScriptId, x.TransactionHash });
                    table.ForeignKey(
                        name: "FK_TransactionScripts_Scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalTable: "Scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionScripts_Transactions_TransactionHash",
                        column: x => x.TransactionHash,
                        principalTable: "Transactions",
                        principalColumn: "Hash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArbitraryData",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<byte[]>(type: "BLOB", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArbitraryData", x => x.Key);
                    table.ForeignKey(
                        name: "FK_ArbitraryData_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LightningPayments",
                columns: table => new
                {
                    PaymentHash = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentId = table.Column<string>(type: "TEXT", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    Inbound = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preimage = table.Column<string>(type: "TEXT", nullable: true),
                    Secret = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightningPayments", x => new { x.WalletId, x.PaymentHash, x.Inbound, x.PaymentId });
                    table.ForeignKey(
                        name: "FK_LightningPayments_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletScripts",
                columns: table => new
                {
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    ScriptId = table.Column<string>(type: "TEXT", nullable: false),
                    DerivationPath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletScripts", x => new { x.WalletId, x.ScriptId });
                    table.ForeignKey(
                        name: "FK_WalletScripts_Scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalTable: "Scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletScripts_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    FundingTransactionHash = table.Column<string>(type: "TEXT", nullable: false),
                    FundingTransactionOutputIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SpendableData = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => new { x.WalletId, x.FundingTransactionHash, x.FundingTransactionOutputIndex });
                    table.ForeignKey(
                        name: "FK_Channels_Coins_FundingTransactionHash_FundingTransactionOutputIndex",
                        columns: x => new { x.FundingTransactionHash, x.FundingTransactionOutputIndex },
                        principalTable: "Coins",
                        principalColumns: new[] { "FundingTransactionHash", "FundingTransactionOutputIndex" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Channels_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArbitraryData_WalletId",
                table: "ArbitraryData",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_FundingTransactionHash_FundingTransactionOutputIndex",
                table: "Channels",
                columns: new[] { "FundingTransactionHash", "FundingTransactionOutputIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Coins_ScriptId",
                table: "Coins",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionScripts_TransactionHash",
                table: "TransactionScripts",
                column: "TransactionHash");

            migrationBuilder.CreateIndex(
                name: "IX_WalletScripts_ScriptId",
                table: "WalletScripts",
                column: "ScriptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArbitraryData");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "LightningPayments");

            migrationBuilder.DropTable(
                name: "TransactionScripts");

            migrationBuilder.DropTable(
                name: "WalletScripts");

            migrationBuilder.DropTable(
                name: "Coins");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Scripts");
        }
    }
}
