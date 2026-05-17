using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecursoId",
                table: "guias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recursos_operadoras_OperadoraId",
                        column: x => x.OperadoraId,
                        principalTable: "operadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recursos_prestadores_PrestadorId",
                        column: x => x.PrestadorId,
                        principalTable: "prestadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guias_RecursoId",
                table: "guias",
                column: "RecursoId");

            migrationBuilder.CreateIndex(
                name: "IX_recursos_OperadoraId",
                table: "recursos",
                column: "OperadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_recursos_PrestadorId",
                table: "recursos",
                column: "PrestadorId");

            migrationBuilder.CreateIndex(
                name: "IX_recursos_TenantId",
                table: "recursos",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_guias_recursos_RecursoId",
                table: "guias",
                column: "RecursoId",
                principalTable: "recursos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_guias_recursos_RecursoId",
                table: "guias");

            migrationBuilder.DropTable(
                name: "recursos");

            migrationBuilder.DropIndex(
                name: "IX_guias_RecursoId",
                table: "guias");

            migrationBuilder.DropColumn(
                name: "RecursoId",
                table: "guias");
        }
    }
}
