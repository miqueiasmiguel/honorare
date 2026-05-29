using App.Catalog;

namespace App.Faturamento;

internal sealed class ItemGuia
{
    public Guid Id { get; private set; }
    public Guid GuiaId { get; private set; }
    public Guid ProcedimentoId { get; private set; }
    public PosicaoExecutor PosicaoExecutor { get; private set; }
    public decimal PercentualOrdem { get; private set; }
    public ViaAcesso ViaAcesso { get; private set; }
    public Acomodacao Acomodacao { get; private set; }
    public bool EhUrgencia { get; private set; }
    public int? TempoAnestesicoMin { get; private set; }
    public decimal? ValorApurado { get; private set; }
    public decimal? ValorLiquidado { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private ItemGuia() { }

    internal void SetValorApurado(decimal? valor) => ValorApurado = valor;

    internal void SetValorLiquidado(decimal? valor) => ValorLiquidado = valor;

    internal void SetTempoAnestesicoMin(int? valor) => TempoAnestesicoMin = valor;

    internal void Atualizar(decimal percentualOrdem, Acomodacao acomodacao, bool ehUrgencia)
    {
        PercentualOrdem = percentualOrdem;
        Acomodacao = acomodacao;
        EhUrgencia = ehUrgencia;
    }

    internal static ItemGuia Create(
        Guid guiaId,
        Guid procedimentoId,
        PosicaoExecutor posicao,
        decimal percentualOrdem,
        ViaAcesso via,
        Acomodacao acomodacao,
        bool ehUrgencia,
        decimal? valorApurado,
        int? tempoAnestesicoMin = null)
    {
        return new ItemGuia
        {
            Id = Guid.NewGuid(),
            GuiaId = guiaId,
            ProcedimentoId = procedimentoId,
            PosicaoExecutor = posicao,
            PercentualOrdem = percentualOrdem,
            ViaAcesso = via,
            Acomodacao = acomodacao,
            EhUrgencia = ehUrgencia,
            TempoAnestesicoMin = tempoAnestesicoMin,
            ValorApurado = valorApurado,
            ValorLiquidado = null,
            CriadoEm = DateTimeOffset.UtcNow,
        };
    }
}
