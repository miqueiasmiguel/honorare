using App.Identity;

namespace App.Catalog;

internal sealed class Procedimento : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string CodigoTuss { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public string? Porte { get; private set; }
    public string? PorteAnestesico { get; private set; }
    public bool EhSadt { get; private set; }
    public bool TemPorteProprioVideo { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Procedimento() { }

    public static Procedimento Create(
        Guid tenantId,
        string codigoTuss,
        string descricao,
        string? porte,
        string? porteAnestesico,
        bool ehSadt,
        bool temPorteProprioVideo)
    {
        return new Procedimento
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CodigoTuss = codigoTuss,
            Descricao = descricao,
            Porte = porte,
            PorteAnestesico = porteAnestesico,
            EhSadt = ehSadt,
            TemPorteProprioVideo = temPorteProprioVideo,
            Ativo = true,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    public void Atualizar(
        string codigoTuss,
        string descricao,
        string? porte,
        string? porteAnestesico,
        bool ehSadt,
        bool temPorteProprioVideo,
        bool ativo)
    {
        CodigoTuss = codigoTuss;
        Descricao = descricao;
        Porte = porte;
        PorteAnestesico = porteAnestesico;
        EhSadt = ehSadt;
        TemPorteProprioVideo = temPorteProprioVideo;
        Ativo = ativo;
    }
}
