using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "deflatores_prestador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Posicao = table.Column<string>(type: "text", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deflatores_prestador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "operadoras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistroAns = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    TipoRuleSet = table.Column<string>(type: "text", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operadoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prestadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistroProfissional = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmailAcesso = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "procedimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoTuss = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Porte = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    PorteAnestesico = table.Column<string>(type: "varchar(2)", nullable: true),
                    EhSadt = table.Column<bool>(type: "boolean", nullable: false),
                    TemPorteProprioVideo = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedimentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacedByTokenId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_ordem_operadora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroProcedimento = table.Column<int>(type: "integer", nullable: false),
                    TipoVia = table.Column<int>(type: "integer", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_ordem_operadora", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_porte_anestesico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    PorteLetra = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ValorEnfermaria = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValorApartamento = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValorAmbulatorial = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_porte_anestesico", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_procedimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_procedimento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "guias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    NumeroGuia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataAtendimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    EhPacote = table.Column<bool>(type: "boolean", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_guias_recursos_RecursoId",
                        column: x => x.RecursoId,
                        principalTable: "recursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "itens_guia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosicaoExecutor = table.Column<string>(type: "text", nullable: false),
                    PercentualOrdem = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    ViaAcesso = table.Column<string>(type: "text", nullable: false),
                    Acomodacao = table.Column<string>(type: "text", nullable: false),
                    EhUrgencia = table.Column<bool>(type: "boolean", nullable: false),
                    TempoAnestesicoMin = table.Column<int>(type: "integer", nullable: true),
                    ValorApurado = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ValorLiquidado = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    motivo_glosa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_GoogleId",
                table: "AspNetUsers",
                column: "GoogleId",
                unique: true,
                filter: "\"GoogleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiarios_TenantId_Carteira",
                table: "beneficiarios",
                columns: new[] { "TenantId", "Carteira" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiarios_TenantId_Nome",
                table: "beneficiarios",
                columns: new[] { "TenantId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_calculos_GuiaId",
                table: "calculos",
                column: "GuiaId");

            migrationBuilder.CreateIndex(
                name: "IX_deflatores_prestador_TenantId_PrestadorId_OperadoraId_Posic~",
                table: "deflatores_prestador",
                columns: new[] { "TenantId", "PrestadorId", "OperadoraId", "Posicao" },
                unique: true);

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
                name: "IX_guias_RecursoId",
                table: "guias",
                column: "RecursoId");

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

            migrationBuilder.CreateIndex(
                name: "IX_operadoras_TenantId_Ativa",
                table: "operadoras",
                columns: new[] { "TenantId", "Ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_operadoras_TenantId_Cnpj",
                table: "operadoras",
                columns: new[] { "TenantId", "Cnpj" },
                unique: true,
                filter: "\"Cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_passos_calculo_CalculoId_Sequencia",
                table: "passos_calculo",
                columns: new[] { "CalculoId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passos_calculo_ItemGuiaId",
                table: "passos_calculo",
                column: "ItemGuiaId");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_TenantId_Ativo",
                table: "prestadores",
                columns: new[] { "TenantId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_procedimentos_TenantId_Ativo",
                table: "procedimentos",
                columns: new[] { "TenantId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_procedimentos_TenantId_CodigoTuss",
                table: "procedimentos",
                columns: new[] { "TenantId", "CodigoTuss" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_ordem_operadora_TenantId_OperadoraId_NumeroProcedim~",
                table: "tabelas_ordem_operadora",
                columns: new[] { "TenantId", "OperadoraId", "NumeroProcedimento", "TipoVia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_porte_anestesico_TenantId_OperadoraId_PorteLetra",
                table: "tabelas_porte_anestesico",
                columns: new[] { "TenantId", "OperadoraId", "PorteLetra" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_procedimento_TenantId_OperadoraId_ProcedimentoId",
                table: "tabelas_procedimento",
                columns: new[] { "TenantId", "OperadoraId", "ProcedimentoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "deflatores_prestador");

            migrationBuilder.DropTable(
                name: "passos_calculo");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "tabelas_ordem_operadora");

            migrationBuilder.DropTable(
                name: "tabelas_porte_anestesico");

            migrationBuilder.DropTable(
                name: "tabelas_procedimento");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "calculos");

            migrationBuilder.DropTable(
                name: "itens_guia");

            migrationBuilder.DropTable(
                name: "guias");

            migrationBuilder.DropTable(
                name: "procedimentos");

            migrationBuilder.DropTable(
                name: "beneficiarios");

            migrationBuilder.DropTable(
                name: "recursos");

            migrationBuilder.DropTable(
                name: "operadoras");

            migrationBuilder.DropTable(
                name: "prestadores");
        }
    }
}
