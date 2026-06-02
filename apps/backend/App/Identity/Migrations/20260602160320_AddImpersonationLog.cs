using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddImpersonationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImpersonationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaasUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationLogs_SaasUserId_TenantId",
                table: "ImpersonationLogs",
                columns: new[] { "SaasUserId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpersonationLogs");
        }
    }
}
