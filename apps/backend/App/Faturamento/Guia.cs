using App.Identity;

namespace App.Faturamento;

internal sealed class Guia : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PrestadorId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public Guid? BeneficiarioId { get; private set; }
    public string Senha { get; private set; } = string.Empty;
    public DateOnly DataAtendimento { get; private set; }
    public SituacaoGuia Situacao { get; private set; }
    public bool EhPacote { get; private set; }
    public string Observacao { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private Guia() { }

    internal static Guia Create(
        Guid tenantId,
        Guid prestadorId,
        Guid operadoraId,
        Guid? beneficiarioId,
        string senha,
        DateOnly dataAtendimento,
        bool ehPacote,
        string observacao)
    {
        var now = DateTimeOffset.UtcNow;
        return new Guia
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrestadorId = prestadorId,
            OperadoraId = operadoraId,
            BeneficiarioId = beneficiarioId,
            Senha = senha.Trim(),
            DataAtendimento = dataAtendimento,
            Situacao = SituacaoGuia.Apresentada,
            EhPacote = ehPacote,
            Observacao = observacao.Trim(),
            CriadoEm = now,
            AtualizadoEm = now,
        };
    }

    internal void Liquidar() => Situacao = SituacaoGuia.Liquidada;

    internal void ReverterParaApresentada() => Situacao = SituacaoGuia.Apresentada;

    internal void Atualizar(
        Guid operadoraId,
        Guid? beneficiarioId,
        string senha,
        DateOnly dataAtendimento,
        bool ehPacote,
        string observacao)
    {
        OperadoraId = operadoraId;
        BeneficiarioId = beneficiarioId;
        Senha = senha;
        DataAtendimento = dataAtendimento;
        EhPacote = ehPacote;
        Observacao = observacao;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }
}
