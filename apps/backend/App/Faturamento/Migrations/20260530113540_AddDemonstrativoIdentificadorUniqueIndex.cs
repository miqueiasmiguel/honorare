using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddDemonstrativoIdentificadorUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_demonstrativos_TenantId_OperadoraId_IdentificadorPagamento",
                table: "demonstrativos",
                columns: new[] { "TenantId", "OperadoraId", "IdentificadorPagamento" },
                unique: true,
                filter: "\"IdentificadorPagamento\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_demonstrativos_TenantId_OperadoraId_IdentificadorPagamento",
                table: "demonstrativos");
        }
    }
}
