using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLDK.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionScript_Scripts_ScriptId",
                table: "TransactionScript");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionScript_Transactions_TransactionHash",
                table: "TransactionScript");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TransactionScript",
                table: "TransactionScript");

            migrationBuilder.RenameTable(
                name: "TransactionScript",
                newName: "TransactionScripts");

            migrationBuilder.RenameIndex(
                name: "IX_TransactionScript_TransactionHash",
                table: "TransactionScripts",
                newName: "IX_TransactionScripts_TransactionHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TransactionScripts",
                table: "TransactionScripts",
                columns: new[] { "ScriptId", "TransactionHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionScripts_Scripts_ScriptId",
                table: "TransactionScripts",
                column: "ScriptId",
                principalTable: "Scripts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionScripts_Transactions_TransactionHash",
                table: "TransactionScripts",
                column: "TransactionHash",
                principalTable: "Transactions",
                principalColumn: "Hash",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionScripts_Scripts_ScriptId",
                table: "TransactionScripts");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionScripts_Transactions_TransactionHash",
                table: "TransactionScripts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TransactionScripts",
                table: "TransactionScripts");

            migrationBuilder.RenameTable(
                name: "TransactionScripts",
                newName: "TransactionScript");

            migrationBuilder.RenameIndex(
                name: "IX_TransactionScripts_TransactionHash",
                table: "TransactionScript",
                newName: "IX_TransactionScript_TransactionHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TransactionScript",
                table: "TransactionScript",
                columns: new[] { "ScriptId", "TransactionHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionScript_Scripts_ScriptId",
                table: "TransactionScript",
                column: "ScriptId",
                principalTable: "Scripts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionScript_Transactions_TransactionHash",
                table: "TransactionScript",
                column: "TransactionHash",
                principalTable: "Transactions",
                principalColumn: "Hash",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
