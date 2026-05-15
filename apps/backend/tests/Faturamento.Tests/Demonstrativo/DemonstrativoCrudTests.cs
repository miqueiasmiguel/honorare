using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.DemonstrativoCrud;

[Collection(nameof(PostgresCollection))]
public sealed class DemonstrativoCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, DemonstrativoService service) BuildService(Guid tenantId)
    {
        var user = new FakeTenantUserDm(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        return (ctx, new DemonstrativoService(ctx, user));
    }

    private static async Task<(Guid operadoraId, Guid prestadorId, Guid procedimentoId)> SeedCatalogAsync(
        AppDbContext ctx, Guid tenantId)
    {
        var op = Operadora.Create(tenantId, "UnimDM02 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        var pr = Prestador.Create(tenantId, "Dr DM02 " + tenantId.ToString("N")[..4], null);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc DM02", "1", null, false, false);
        ctx.Add(op);
        ctx.Add(pr);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();
        return (op.Id, pr.Id, proc.Id);
    }

    [Fact]
    public async Task Criar_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var cmd = new CriarDemonstrativoCommand(operadoraId, "2025-12",
            new DateOnly(2025, 12, 10), "Obs DM-02");

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(operadoraId, result.Value!.Header.OperadoraId);
        Assert.Equal("2025-12", result.Value.Header.Competencia);
        Assert.Equal(new DateOnly(2025, 12, 10), result.Value.Header.DataRecebimento);
        Assert.Equal("Obs DM-02", result.Value.Header.Observacao);
        Assert.Empty(result.Value.Itens);
    }

    [Fact]
    public async Task Listar_FiltroPorOperadoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var opOutra = Operadora.Create(tenantId, "Outra DM02 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        ctx.Add(opOutra);
        await ctx.SaveChangesAsync();

        await service.CriarAsync(new CriarDemonstrativoCommand(operadoraId, "2025-11", new DateOnly(2025, 11, 1), null));
        await service.CriarAsync(new CriarDemonstrativoCommand(opOutra.Id, "2025-11", new DateOnly(2025, 11, 1), null));

        var result = await service.ListarAsync(new ListarDemonstrativosQuery(operadoraId, null, 1, 20));

        Assert.Single(result.Itens);
        Assert.Equal(operadoraId, result.Itens[0].OperadoraId);
    }

    [Fact]
    public async Task Listar_FiltroPorCompetenciaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        await service.CriarAsync(new CriarDemonstrativoCommand(operadoraId, "2025-10", new DateOnly(2025, 10, 1), null));
        await service.CriarAsync(new CriarDemonstrativoCommand(operadoraId, "2025-11", new DateOnly(2025, 11, 1), null));

        var result = await service.ListarAsync(new ListarDemonstrativosQuery(null, "2025-10", 1, 20));

        Assert.Single(result.Itens);
        Assert.Equal("2025-10", result.Itens[0].Competencia);
    }

    [Fact]
    public async Task Listar_PaginacaoFuncionaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        for (var i = 1; i <= 5; i++)
        {
            await service.CriarAsync(new CriarDemonstrativoCommand(
                operadoraId, $"2025-0{i}", new DateOnly(2025, i, 1), null));
        }

        var pagina1 = await service.ListarAsync(new ListarDemonstrativosQuery(null, null, 1, 2));
        var pagina2 = await service.ListarAsync(new ListarDemonstrativosQuery(null, null, 2, 2));

        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.True(pagina1.Total >= 5);
        Assert.NotEqual(pagina1.Itens[0].Id, pagina2.Itens[0].Id);
    }

    [Fact]
    public async Task Atualizar_CamposAtualizadosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-11", new DateOnly(2025, 11, 1), "Antiga"));
        Assert.True(criado.IsSuccess);

        var atualizado = await service.AtualizarAsync(criado.Value!.Header.Id,
            new AtualizarDemonstrativoCommand(operadoraId, "2025-12", new DateOnly(2025, 12, 5), "Nova"));

        Assert.True(atualizado.IsSuccess);
        Assert.Equal("2025-12", atualizado.Value!.Header.Competencia);
        Assert.Equal(new DateOnly(2025, 12, 5), atualizado.Value.Header.DataRecebimento);
        Assert.Equal("Nova", atualizado.Value.Header.Observacao);
    }

    [Fact]
    public async Task Excluir_SemItens_RemovidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-09", new DateOnly(2025, 9, 1), null));
        Assert.True(criado.IsSuccess);
        var demId = criado.Value!.Header.Id;

        var result = await service.ExcluirAsync(demId);

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        Assert.Null(await adminCtx.Demonstrativos.FirstOrDefaultAsync(d => d.Id == demId));
    }

    [Fact]
    public async Task Excluir_ComItemConciliado_Lanca409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-08", new DateOnly(2025, 8, 1), null));
        Assert.True(criado.IsSuccess);
        var demId = criado.Value!.Header.Id;

        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "DM02EXC" + tenantId.ToString("N")[..5], new DateOnly(2025, 8, 1), false, string.Empty);
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        var itemGuia = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(itemGuia);
        await ctx.SaveChangesAsync();

        var addItem = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SENHA01", "40300390", null, 500m, 500m, null));
        Assert.True(addItem.IsSuccess);
        var itemDemId = addItem.Value!.Itens[0].Id;

        await using var ctx2 = db.CreateTenantContext(tenantId);
        var itemDem = await ctx2.ItensDemonstrativo.FindAsync(itemDemId);
        itemDem!.Conciliar(itemGuia.Id);
        await ctx2.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExcluirAsync(demId));
    }

    [Fact]
    public async Task AdicionarItem_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-07", new DateOnly(2025, 7, 1), null));
        Assert.True(criado.IsSuccess);
        var demId = criado.Value!.Header.Id;

        var result = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SENHAITEM", "40300391", "Desc item", 1000m, 800m, "Glosa parcial"));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Itens);
        var item = result.Value.Itens[0];
        Assert.Equal("SENHAITEM", item.Senha);
        Assert.Equal("40300391", item.CodigoTuss);
        Assert.Equal(200m, item.ValorGlosado);
        Assert.Equal("Glosa parcial", item.MotivoGlosa);
        Assert.Null(item.ItemGuiaId);
        Assert.False(item.Conciliado);
    }

    [Fact]
    public async Task RemoverItem_NaoConciliado_RemovidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-06", new DateOnly(2025, 6, 1), null));
        var demId = criado.Value!.Header.Id;

        var addItem = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SEN-RM", "40300392", null, 300m, 300m, null));
        var itemId = addItem.Value!.Itens[0].Id;

        var result = await service.RemoverItemAsync(demId, itemId);

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        Assert.Null(await adminCtx.ItensDemonstrativo.FirstOrDefaultAsync(i => i.Id == itemId));
    }

    [Fact]
    public async Task RemoverItem_Conciliado_Lanca409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-05", new DateOnly(2025, 5, 1), null));
        var demId = criado.Value!.Header.Id;

        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "DM02RM" + tenantId.ToString("N")[..6], new DateOnly(2025, 5, 1), false, string.Empty);
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        var itemGuia = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(itemGuia);
        await ctx.SaveChangesAsync();

        var addItem = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SEN-RMC", "40300393", null, 200m, 200m, null));
        var itemDemId = addItem.Value!.Itens[0].Id;

        await using var ctx2 = db.CreateTenantContext(tenantId);
        var itemDem = await ctx2.ItensDemonstrativo.FindAsync(itemDemId);
        itemDem!.Conciliar(itemGuia.Id);
        await ctx2.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoverItemAsync(demId, itemDemId));
    }
}

file sealed class FakeTenantUserDm(Guid tenantId) : App.Identity.ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
