using System.Globalization;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Schema;

[Collection(nameof(PostgresCollection))]
public sealed class CatalogSchemaTests(PostgresContainerFixture db)
{
    // ── Existência das tabelas ───────────────────────────────────────────────

    [Fact]
    public async Task Tabela_Operadoras_Existe_Async()
    {
        var count = await ContarTabelasAsync("operadoras");
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Tabela_Procedimentos_Existe_Async()
    {
        var count = await ContarTabelasAsync("procedimentos");
        Assert.Equal(1L, count);
    }

    // ── Índices únicos (integridade de dados multi-tenant) ───────────────────

    [Fact]
    public async Task Operadoras_IndiceUnico_TenantId_Cnpj_Async()
    {
        var count = await ContarIndicesAsync(
            tableName: "operadoras",
            mustBeUnique: true,
            likePatterns: ["%TenantId%", "%Cnpj%"]);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Procedimentos_IndiceUnico_TenantId_CodigoTuss_Async()
    {
        var count = await ContarIndicesAsync(
            tableName: "procedimentos",
            mustBeUnique: true,
            likePatterns: ["%TenantId%", "%CodigoTuss%"]);
        Assert.Equal(1L, count);
    }

    // ── Constraints NOT NULL em campos obrigatórios ──────────────────────────

    [Fact]
    public async Task Operadoras_ColunaNome_NaoNula_Async()
    {
        var nullable = await ObterNullabilityAsync("operadoras", "Nome");
        Assert.Equal("NO", nullable);
    }

    [Fact]
    public async Task Operadoras_ColunaTenantId_NaoNula_Async()
    {
        var nullable = await ObterNullabilityAsync("operadoras", "TenantId");
        Assert.Equal("NO", nullable);
    }

    [Fact]
    public async Task Procedimentos_ColunaCodigoTuss_NaoNula_Async()
    {
        var nullable = await ObterNullabilityAsync("procedimentos", "CodigoTuss");
        Assert.Equal("NO", nullable);
    }

    [Fact]
    public async Task Procedimentos_ColunaDescricao_NaoNula_Async()
    {
        var nullable = await ObterNullabilityAsync("procedimentos", "Descricao");
        Assert.Equal("NO", nullable);
    }

    // ── Tipo de armazenamento do enum TipoRuleSet ────────────────────────────

    [Fact]
    public async Task Operadoras_ColunaTipoRuleSet_ArmazenadaComoTexto_Async()
    {
        var type = await ObterTipoColunaAsync("operadoras", "TipoRuleSet");
        // HasConversion<string>() → character varying ou text no PostgreSQL
        Assert.True(
            type is "character varying" or "text",
            $"Esperado character varying ou text, obtido: {type}");
    }

    // ── Colunas opcionais são nullable ───────────────────────────────────────

    [Fact]
    public async Task Operadoras_ColunaCnpj_Nullable_Async()
    {
        var nullable = await ObterNullabilityAsync("operadoras", "Cnpj");
        Assert.Equal("YES", nullable);
    }

    [Fact]
    public async Task Operadoras_ColunaRegistroAns_Nullable_Async()
    {
        var nullable = await ObterNullabilityAsync("operadoras", "RegistroAns");
        Assert.Equal("YES", nullable);
    }

    [Fact]
    public async Task Procedimentos_ColunaPorteAnestesico_Nullable_Async()
    {
        var nullable = await ObterNullabilityAsync("procedimentos", "PorteAnestesico");
        Assert.Equal("YES", nullable);
    }

    // ── Índices de filtragem (TenantId + status) ─────────────────────────────

    [Fact]
    public async Task Operadoras_IndiceComposto_TenantId_Ativa_Async()
    {
        var count = await ContarIndicesAsync(
            tableName: "operadoras",
            mustBeUnique: false,
            likePatterns: ["%TenantId%", "%Ativa%"]);
        Assert.True(count >= 1L, "Índice composto (TenantId, Ativa) não encontrado em operadoras");
    }

    [Fact]
    public async Task Procedimentos_IndiceComposto_TenantId_Ativo_Async()
    {
        var count = await ContarIndicesAsync(
            tableName: "procedimentos",
            mustBeUnique: false,
            likePatterns: ["%TenantId%", "%Ativo%"]);
        Assert.True(count >= 1L, "Índice composto (TenantId, Ativo) não encontrado em procedimentos");
    }

    // ── CodigoTuss respeita max length 10 (character varying(10)) ────────────

    [Fact]
    public async Task Procedimentos_ColunaCodigoTuss_MaxLength10_Async()
    {
        var maxLen = await ObterMaxLengthAsync("procedimentos", "CodigoTuss");
        Assert.Equal(10L, maxLen);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<long> ContarTabelasAsync(string tableName)
    {
        using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100 // SQL é 100% hardcoded — sem user input
        cmd.CommandText =
            $"SELECT COUNT(*) FROM information_schema.tables " +
            $"WHERE table_schema = 'public' AND table_name = '{tableName}'";
#pragma warning restore CA2100
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> ContarIndicesAsync(string tableName, bool mustBeUnique, string[] likePatterns)
    {
        using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();

        var uniquePart = mustBeUnique
            ? "AND indexdef LIKE 'CREATE UNIQUE INDEX%'"
            : "AND indexdef NOT LIKE 'CREATE UNIQUE INDEX%'";

        var likeConditions = string.Join(
            " ",
            likePatterns.Select(static p => $"AND indexdef LIKE '{p}'"));

#pragma warning disable CA2100
        cmd.CommandText =
            $"SELECT COUNT(*) FROM pg_indexes " +
            $"WHERE tablename = '{tableName}' {uniquePart} {likeConditions}";
#pragma warning restore CA2100

        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<string?> ObterNullabilityAsync(string tableName, string columnName)
    {
        using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100
        cmd.CommandText =
            $"SELECT is_nullable FROM information_schema.columns " +
            $"WHERE table_name = '{tableName}' AND column_name = '{columnName}'";
#pragma warning restore CA2100
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private async Task<string?> ObterTipoColunaAsync(string tableName, string columnName)
    {
        using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100
        cmd.CommandText =
            $"SELECT data_type FROM information_schema.columns " +
            $"WHERE table_name = '{tableName}' AND column_name = '{columnName}'";
#pragma warning restore CA2100
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private async Task<long?> ObterMaxLengthAsync(string tableName, string columnName)
    {
        using var ctx = db.CreateContext();
        await ctx.Database.OpenConnectionAsync();
        using var cmd = ctx.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100
        cmd.CommandText =
            $"SELECT character_maximum_length FROM information_schema.columns " +
            $"WHERE table_name = '{tableName}' AND column_name = '{columnName}'";
#pragma warning restore CA2100
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null
            ? null
            : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
