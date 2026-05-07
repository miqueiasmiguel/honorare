using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beneficiarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Carteira = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_beneficiarios_TenantId_Carteira",
                table: "beneficiarios",
                columns: new[] { "TenantId", "Carteira" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiarios_TenantId_Nome",
                table: "beneficiarios",
                columns: new[] { "TenantId", "Nome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beneficiarios");
        }
    }
}
