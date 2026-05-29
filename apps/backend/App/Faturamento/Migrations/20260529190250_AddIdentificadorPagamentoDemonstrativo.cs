using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentificadorPagamentoDemonstrativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentificadorPagamento",
                table: "demonstrativos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentificadorPagamento",
                table: "demonstrativos");
        }
    }
}
