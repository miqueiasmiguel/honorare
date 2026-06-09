using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using App.Storage;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class RecursoItemInclusaoTests(PostgresContainerFixture db)
{
    private static readonly IFileStorage _noopStorage = new RecursoItemNoopFileStorage();

    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeRecursoItemTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        return (ctx, currentUser);
    }

    private static async Task<(Guid opId, Guid prestId, Guid procId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. EIR " + tenantId.ToString("N")[..4], null);
        var operadora = Operadora.Create(tenantId, "UNIMED EIR " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc EIR", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        await ctx.SaveChangesAsync();

        return (operadora.Id, prestador.Id, procedimento.Id);
    }

    private static async Task<Guid> CriarGuiaAsync(
        AppDbContext ctx, ICurrentUser user,
        Guid prestadorId, Guid operadoraId, Guid procedimentoId,
        string numeroGuia)
    {
        var factory = new PricingRuleSetFactory(ctx);
        var svc = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, null, numeroGuia,
            new DateOnly(2026, 1, 10), false, string.Empty,
            [new CriarItemGuiaCommand(
                procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);
        var result = await svc.CriarAsync(cmd);
        return result.Value!.Id;
    }

    [Fact]
    public async Task ExcluirItem_Guia2Itens_MarcaIncluidoFalseAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100001"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-EX-" + tenantId.ToString("N")[..4]);

        ctx.Add(ItemGuia.Create(guiaId, procId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        var itemParaExcluir = itens[0];

        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemParaExcluir.Id, false);

        await using var adminCtx = db.CreateContext();
        var itemAtualizado = await adminCtx.ItensGuia.FirstAsync(i => i.Id == itemParaExcluir.Id);
        Assert.False(itemAtualizado.IncluidoNoRecurso);
    }

    [Fact]
    public async Task ReincluirItem_VoltaIncluidoTrueAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100002"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-REI-" + tenantId.ToString("N")[..4]);

        ctx.Add(ItemGuia.Create(guiaId, procId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        var itemId = itens[0].Id;

        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemId, false);
        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemId, true);

        await using var adminCtx = db.CreateContext();
        var itemAtualizado = await adminCtx.ItensGuia.FirstAsync(i => i.Id == itemId);
        Assert.True(itemAtualizado.IncluidoNoRecurso);
    }

    [Fact]
    public async Task ExcluirUltimoItemIncluido_LancaInvalidOperationExceptionAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100003"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-ULT-" + tenantId.ToString("N")[..4]);
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var item = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AlterarInclusaoItemAsync(recursoId, guiaId, item.Id, false));
    }

    [Fact]
    public async Task ObterPorId_ExpoesIncluidoNoRecursoNoDtoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100004"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-DTO-" + tenantId.ToString("N")[..4]);

        ctx.Add(ItemGuia.Create(guiaId, procId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        var itemParaExcluir = itens[0];
        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemParaExcluir.Id, false);

        var detalhe = await service.ObterPorIdAsync(recursoId);
        var guiaDto = detalhe.Value!.Guias[0];
        var itemExcluido = guiaDto.Itens.First(i => i.Id == itemParaExcluir.Id);
        Assert.False(itemExcluido.IncluidoNoRecurso);
        var itemIncluido = guiaDto.Itens.First(i => i.Id != itemParaExcluir.Id);
        Assert.True(itemIncluido.IncluidoNoRecurso);
    }

    [Fact]
    public async Task ObterDadosPdf_OmiteItemExcluidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100005"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-PDF-" + tenantId.ToString("N")[..4]);

        ctx.Add(ItemGuia.Create(guiaId, procId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        var itemParaExcluir = itens[0];
        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemParaExcluir.Id, false);

        var pdf = await service.ObterDadosPdfAsync(recursoId);
        Assert.Single(pdf.Value!.Guias[0].Itens);
    }

    [Fact]
    public async Task RemoverGuia_ResetaIncluidoNoRecursoParaTrueAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "100006"))).Value!.Id;
        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, "EIR-RST-" + tenantId.ToString("N")[..4]);

        ctx.Add(ItemGuia.Create(guiaId, procId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        var itemParaExcluir = itens[0];
        await service.AlterarInclusaoItemAsync(recursoId, guiaId, itemParaExcluir.Id, false);

        await service.RemoverGuiaAsync(recursoId, guiaId);

        await using var adminCtx = db.CreateContext();
        var todosItens = await adminCtx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        Assert.All(todosItens, i => Assert.True(i.IncluidoNoRecurso));
    }
}

file sealed class FakeRecursoItemTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}

file sealed class RecursoItemNoopFileStorage : IFileStorage
{
    public Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<FileStorageObject?>(null);

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        Task.CompletedTask;
}
