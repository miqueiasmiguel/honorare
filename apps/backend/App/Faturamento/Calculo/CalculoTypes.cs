using App.Catalog;

namespace App.Faturamento.Motor;

internal enum SituacaoApuracao
{
    Calculado,
    SemTabela,
    Indeterminado,
}

internal sealed record PassoApuracao(string Regra, decimal Fator, decimal ValorResultante);

internal sealed record ApuracaoItemResult(
    Guid ItemGuiaId,
    SituacaoApuracao Situacao,
    decimal? ValorApurado,
    decimal PercentualOrdem,
    IReadOnlyList<PassoApuracao> Passos);

internal sealed record ApurarItemInput(
    Guid ItemGuiaId,
    Guid ProcedimentoId,
    PosicaoExecutor Posicao,
    ViaAcesso Via,
    Acomodacao Acomodacao,
    bool EhUrgencia,
    int? TempoAnestesicoMin = null);

internal sealed record ApurarGuiaContext(
    Guid TenantId,
    Guid PrestadorId,
    Guid OperadoraId,
    IReadOnlyList<ApurarItemInput> Itens);
