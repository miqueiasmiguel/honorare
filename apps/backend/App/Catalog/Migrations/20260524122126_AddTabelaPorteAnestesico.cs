using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddTabelaPorteAnestesico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tabelas_porte_anestesico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    PorteLetra = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ValorEnfermaria = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValorApartamento = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValorAmbulatorial = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_porte_anestesico", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_porte_anestesico_TenantId_OperadoraId_PorteLetra",
                table: "tabelas_porte_anestesico",
                columns: new[] { "TenantId", "OperadoraId", "PorteLetra" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tabelas_porte_anestesico");
        }
    }
}
