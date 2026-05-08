using App.Catalog;

namespace App.Faturamento;

internal sealed class ItemGuia
{
    public Guid Id { get; private set; }
    public Guid GuiaId { get; private set; }
    public Guid ProcedimentoId { get; private set; }
    public PosicaoExecutor PosicaoExecutor { get; private set; }
    public OrdemProcedimento OrdemProcedimento { get; private set; }
    public ViaAcesso ViaAcesso { get; private set; }
    public Acomodacao Acomodacao { get; private set; }
    public bool EhUrgencia { get; private set; }
    public decimal? ValorApurado { get; private set; }
    public decimal? ValorLiquidado { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private ItemGuia() { }

    internal void SetValorApurado(decimal? valor) => ValorApurado = valor;

    internal static ItemGuia Create(
        Guid guiaId,
        Guid procedimentoId,
        PosicaoExecutor posicao,
        OrdemProcedimento ordem,
        ViaAcesso via,
        Acomodacao acomodacao,
        bool ehUrgencia,
        decimal? valorApurado)
    {
        return new ItemGuia
        {
            Id = Guid.NewGuid(),
            GuiaId = guiaId,
            ProcedimentoId = procedimentoId,
            PosicaoExecutor = posicao,
            OrdemProcedimento = ordem,
            ViaAcesso = via,
            Acomodacao = acomodacao,
            EhUrgencia = ehUrgencia,
            ValorApurado = valorApurado,
            ValorLiquidado = null,
            CriadoEm = DateTimeOffset.UtcNow,
        };
    }
}
