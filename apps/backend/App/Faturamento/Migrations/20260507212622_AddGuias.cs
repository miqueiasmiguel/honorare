using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddGuias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Senha = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DataAtendimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    EhPacote = table.Column<bool>(type: "boolean", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guias_beneficiarios_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "beneficiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guias_operadoras_OperadoraId",
                        column: x => x.OperadoraId,
                        principalTable: "operadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guias_prestadores_PrestadorId",
                        column: x => x.PrestadorId,
                        principalTable: "prestadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_guia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosicaoExecutor = table.Column<string>(type: "text", nullable: false),
                    OrdemProcedimento = table.Column<string>(type: "text", nullable: false),
                    ViaAcesso = table.Column<string>(type: "text", nullable: false),
                    Acomodacao = table.Column<string>(type: "text", nullable: false),
                    EhUrgencia = table.Column<bool>(type: "boolean", nullable: false),
                    ValorApurado = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ValorLiquidado = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_guia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_guia_guias_GuiaId",
                        column: x => x.GuiaId,
                        principalTable: "guias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itens_guia_procedimentos_ProcedimentoId",
                        column: x => x.ProcedimentoId,
                        principalTable: "procedimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guias_BeneficiarioId",
                table: "guias",
                column: "BeneficiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_guias_OperadoraId",
                table: "guias",
                column: "OperadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_guias_PrestadorId",
                table: "guias",
                column: "PrestadorId");

            migrationBuilder.CreateIndex(
                name: "IX_guias_TenantId",
                table: "guias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_guia_GuiaId",
                table: "itens_guia",
                column: "GuiaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_guia_ProcedimentoId",
                table: "itens_guia",
                column: "ProcedimentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_guia");

            migrationBuilder.DropTable(
                name: "guias");
        }
    }
}
