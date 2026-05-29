using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrdemProcedimentoToPercentualOrdem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrdemProcedimento",
                table: "itens_guia");

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualOrdem",
                table: "itens_guia",
                type: "numeric(5,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PercentualOrdem",
                table: "itens_guia");

            migrationBuilder.AddColumn<string>(
                name: "OrdemProcedimento",
                table: "itens_guia",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
