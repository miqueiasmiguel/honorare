using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.AdicionarItem;

[Collection(nameof(PostgresCollection))]
public sealed class AdicionarItemTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user, PricingRuleSetFactory factory) BuildTenant(Guid tenantId)
    {
        var user = new FakeAddItemUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        return (ctx, user, new PricingRuleSetFactory(ctx));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedBaseAsync(AppDbContext ctx, Guid tenantId, TipoRuleSet tipoRuleSet = TipoRuleSet.Unimed)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Add", null);
        var operadora = Operadora.Create(tenantId, "Op " + tenantId.ToString("N")[..6], null, null, tipoRuleSet);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Add", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();
        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    private static CriarItemGuiaCommand ItemCmd(Guid procedimentoId, decimal? valorApurado = null) =>
        new(procedimentoId, PosicaoExecutor.Cirurgiao,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, valorApurado);

    private static async Task<GuiaDetalheDto> CriarGuiaAsync(
        GuiaService service, Guid prestadorId, Guid operadoraId, Guid procedimentoId,
        Guid tenantId, bool ehPacote = false, decimal? valorApuradoItem = null)
    {
        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null,
            "ADD" + tenantId.ToString("N")[..5], new DateOnly(2025, 1, 1), ehPacote, string.Empty,
            [ItemCmd(procedimentoId, valorApuradoItem)]);
        var result = await service.CriarAsync(cmd);
        return result.Value!;
    }

    [Fact]
    public async Task AdicionarItem_Unimed_Apuravel_PreencheValorApuradoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(service, prestadorId, operadoraId, procedimentoId, tenantId);
        var itemOriginalId = guia.Itens[0].Id;

        var result = await service.AdicionarItemAsync(guia.Id, ItemCmd(procedimentoId));

        Assert.True(result.IsSuccess);
        var novoItem = result.Value!.Itens.Single(i => i.Id != itemOriginalId);
        Assert.NotNull(novoItem.ValorApurado);

        await using var adminCtx = db.CreateContext();
        Assert.True(await adminCtx.PassosCalculo
            .AnyAsync(p => p.ItemGuiaId == novoItem.Id && p.Regra == "ValorBase"));
    }

    [Fact]
    public async Task Deve_reapurar_guia_inteira_ao_adicionar_itemAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var proc2 = Procedimento.Create(tenantId, tenantId.ToString("N")[8..16], "Proc2 Add", "1", null, false, false);
        ctx.Add(proc2);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 200m));
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc2.Id, 500m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(service, prestadorId, operadoraId, procedimentoId, tenantId);
        var itemOriginalId = guia.Itens[0].Id;
        Assert.Equal(200m, guia.Itens[0].ValorApurado); // rank 0 = 100%

        // Adiciona proc2 (500m) — deve virar rank 0 = 100%; proc1 (200m) cai para rank 1 = 50%
        var result = await service.AdicionarItemAsync(guia.Id,
            new CriarItemGuiaCommand(proc2.Id, PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));

        Assert.True(result.IsSuccess);
        var itens = result.Value!.Itens;
        Assert.Equal(500m, itens.First(i => i.ProcedimentoId == proc2.Id).ValorApurado);    // rank 0 = 100%
        Assert.Equal(100m, itens.First(i => i.Id == itemOriginalId).ValorApurado);           // rank 1 = 50%
    }

    [Fact]
    public async Task AdicionarItem_PreservaValorLiquidadoEMotivoGlosaDosItensExistentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) =
            await SeedBaseAsync(ctx, tenantId, TipoRuleSet.Nulo);

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(service, prestadorId, operadoraId, procedimentoId, tenantId);
        var itemOriginalId = guia.Itens[0].Id;
        await service.AtualizarPagamentoItemAsync(guia.Id, itemOriginalId, 100m, "CB");

        var result = await service.AdicionarItemAsync(guia.Id, ItemCmd(procedimentoId));

        Assert.True(result.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var itemOriginal = await check.ItensGuia.FirstAsync(i => i.Id == itemOriginalId);
        Assert.Equal(100m, itemOriginal.ValorLiquidado);
        Assert.Equal("CB", itemOriginal.MotivoGlosa);
    }

    [Fact]
    public async Task AdicionarItem_SemTabela_GuiaNaoPacote_RejeitaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(service, prestadorId, operadoraId, procedimentoId, tenantId);

        var procSemTabela = Procedimento.Create(
            tenantId, tenantId.ToString("N")[..8] + "9", "Sem Tabela", "1", null, false, false);
        ctx.Add(procSemTabela);
        await ctx.SaveChangesAsync();

        var result = await service.AdicionarItemAsync(guia.Id, ItemCmd(procSemTabela.Id));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task AdicionarItem_Pacote_SemValorApurado_RejeitaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(
            service, prestadorId, operadoraId, procedimentoId, tenantId, ehPacote: true, valorApuradoItem: 500m);

        var result = await service.AdicionarItemAsync(guia.Id, ItemCmd(procedimentoId, valorApurado: null));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task AdicionarItem_Pacote_ValorManual_NaoInvocaMotorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var service = new GuiaService(ctx, user, factory);
        var guia = await CriarGuiaAsync(
            service, prestadorId, operadoraId, procedimentoId, tenantId, ehPacote: true, valorApuradoItem: 500m);
        var itemOriginalId = guia.Itens[0].Id;

        var result = await service.AdicionarItemAsync(guia.Id, ItemCmd(procedimentoId, valorApurado: 500m));

        Assert.True(result.IsSuccess);
        var novoItem = result.Value!.Itens.Single(i => i.Id != itemOriginalId);
        Assert.Equal(500m, novoItem.ValorApurado);

        await using var adminCtx = db.CreateContext();
        Assert.False(await adminCtx.PassosCalculo.AnyAsync(p => p.ItemGuiaId == novoItem.Id));
    }

    [Fact]
    public async Task AdicionarItem_GuiaInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (_, _, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var service = new GuiaService(ctx, user, factory);

        var result = await service.AdicionarItemAsync(Guid.NewGuid(), ItemCmd(procedimentoId));

        Assert.False(result.IsSuccess);
        Assert.IsType<NotFoundError>(result.Error);
    }
}

file sealed class FakeAddItemUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
