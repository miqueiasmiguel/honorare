using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLogoKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoKey",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoKey",
                table: "Tenants");
        }
    }
}
