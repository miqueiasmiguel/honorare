using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddTabelasPrestadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deflatores_prestador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Posicao = table.Column<string>(type: "text", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deflatores_prestador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prestadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistroProfissional = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_procedimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_procedimento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deflatores_prestador_TenantId_PrestadorId_OperadoraId_Posic~",
                table: "deflatores_prestador",
                columns: new[] { "TenantId", "PrestadorId", "OperadoraId", "Posicao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_TenantId_Ativo",
                table: "prestadores",
                columns: new[] { "TenantId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_procedimento_TenantId_OperadoraId_ProcedimentoId",
                table: "tabelas_procedimento",
                columns: new[] { "TenantId", "OperadoraId", "ProcedimentoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deflatores_prestador");

            migrationBuilder.DropTable(
                name: "prestadores");

            migrationBuilder.DropTable(
                name: "tabelas_procedimento");
        }
    }
}
