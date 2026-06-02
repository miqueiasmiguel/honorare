using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddActingTenantIdToRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActingTenantId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActingTenantId",
                table: "RefreshTokens");
        }
    }
}
