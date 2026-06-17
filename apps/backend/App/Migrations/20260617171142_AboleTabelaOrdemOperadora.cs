using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AboleTabelaOrdemOperadora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tabelas_ordem_operadora");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tabelas_ordem_operadora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroProcedimento = table.Column<int>(type: "integer", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoVia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_ordem_operadora", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_ordem_operadora_TenantId_OperadoraId_NumeroProcedim~",
                table: "tabelas_ordem_operadora",
                columns: new[] { "TenantId", "OperadoraId", "NumeroProcedimento", "TipoVia" },
                unique: true);
        }
    }
}
