using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class RenomearSenhaParaNumeroGuia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropa o campo NumeroGuia que era opcional e nunca foi preenchido
            migrationBuilder.DropColumn(
                name: "NumeroGuia",
                table: "guias");

            // Renomeia Senha → NumeroGuia preservando todos os dados
            migrationBuilder.RenameColumn(
                name: "Senha",
                table: "guias",
                newName: "NumeroGuia");

            // Alarga de varchar(30) para varchar(50)
            migrationBuilder.AlterColumn<string>(
                name: "NumeroGuia",
                table: "guias",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NumeroGuia",
                table: "guias",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.RenameColumn(
                name: "NumeroGuia",
                table: "guias",
                newName: "Senha");

            migrationBuilder.AddColumn<string>(
                name: "NumeroGuia",
                table: "guias",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
