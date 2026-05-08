using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class AddCalculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RealizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calculos_guias_GuiaId",
                        column: x => x.GuiaId,
                        principalTable: "guias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "passos_calculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemGuiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<int>(type: "integer", nullable: false),
                    Regra = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Fator = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    ValorResultante = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passos_calculo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passos_calculo_calculos_CalculoId",
                        column: x => x.CalculoId,
                        principalTable: "calculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_passos_calculo_itens_guia_ItemGuiaId",
                        column: x => x.ItemGuiaId,
                        principalTable: "itens_guia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calculos_GuiaId",
                table: "calculos",
                column: "GuiaId");

            migrationBuilder.CreateIndex(
                name: "IX_passos_calculo_CalculoId_Sequencia",
                table: "passos_calculo",
                columns: new[] { "CalculoId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passos_calculo_ItemGuiaId",
                table: "passos_calculo",
                column: "ItemGuiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "passos_calculo");

            migrationBuilder.DropTable(
                name: "calculos");
        }
    }
}
