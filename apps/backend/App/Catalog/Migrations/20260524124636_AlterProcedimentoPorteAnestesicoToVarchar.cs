using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AlterProcedimentoPorteAnestesicoToVarchar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PorteAnestesico",
                table: "procedimentos",
                type: "varchar(2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PorteAnestesico",
                table: "procedimentos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldNullable: true);
        }
    }
}
