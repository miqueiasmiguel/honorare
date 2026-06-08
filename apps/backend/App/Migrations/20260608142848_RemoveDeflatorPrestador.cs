using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeflatorPrestador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deflatores_prestador");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deflatores_prestador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    Posicao = table.Column<string>(type: "text", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deflatores_prestador", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deflatores_prestador_TenantId_PrestadorId_OperadoraId_Posic~",
                table: "deflatores_prestador",
                columns: new[] { "TenantId", "PrestadorId", "OperadoraId", "Posicao" },
                unique: true);
        }
    }
}
