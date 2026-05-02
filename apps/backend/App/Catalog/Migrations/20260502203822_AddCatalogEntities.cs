using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operadoras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistroAns = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    TipoRuleSet = table.Column<string>(type: "text", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operadoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "procedimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoTuss = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Porte = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    PorteAnestesico = table.Column<int>(type: "integer", nullable: true),
                    EhSadt = table.Column<bool>(type: "boolean", nullable: false),
                    TemPorteProprioVideo = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedimentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operadoras_TenantId_Ativa",
                table: "operadoras",
                columns: new[] { "TenantId", "Ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_operadoras_TenantId_Cnpj",
                table: "operadoras",
                columns: new[] { "TenantId", "Cnpj" },
                unique: true,
                filter: "\"Cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_procedimentos_TenantId_Ativo",
                table: "procedimentos",
                columns: new[] { "TenantId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_procedimentos_TenantId_CodigoTuss",
                table: "procedimentos",
                columns: new[] { "TenantId", "CodigoTuss" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operadoras");

            migrationBuilder.DropTable(
                name: "procedimentos");
        }
    }
}
