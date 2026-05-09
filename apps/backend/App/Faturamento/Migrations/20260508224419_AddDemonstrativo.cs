using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddDemonstrativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demonstrativos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competencia = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    DataRecebimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demonstrativos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_demonstrativos_operadoras_OperadoraId",
                        column: x => x.OperadoraId,
                        principalTable: "operadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_demonstrativo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemonstrativoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Senha = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CodigoTuss = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValorApresentado = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ValorPago = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ValorGlosado = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    MotivoGlosa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemGuiaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_demonstrativo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_demonstrativo_demonstrativos_DemonstrativoId",
                        column: x => x.DemonstrativoId,
                        principalTable: "demonstrativos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itens_demonstrativo_itens_guia_ItemGuiaId",
                        column: x => x.ItemGuiaId,
                        principalTable: "itens_guia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demonstrativos_OperadoraId",
                table: "demonstrativos",
                column: "OperadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_demonstrativos_TenantId",
                table: "demonstrativos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_demonstrativo_DemonstrativoId",
                table: "itens_demonstrativo",
                column: "DemonstrativoId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_demonstrativo_ItemGuiaId",
                table: "itens_demonstrativo",
                column: "ItemGuiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_demonstrativo");

            migrationBuilder.DropTable(
                name: "demonstrativos");
        }
    }
}
