using App.Identity;

namespace App.Catalog;

internal sealed class TabelaPorteAnestesico : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public string PorteLetra { get; private set; } = string.Empty;
    public decimal ValorEnfermaria { get; private set; }
    public decimal ValorApartamento { get; private set; }
    public decimal? ValorAmbulatorial { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private TabelaPorteAnestesico() { }

    public static TabelaPorteAnestesico Create(
        Guid tenantId,
        Guid operadoraId,
        string porteLetra,
        decimal valorEnfermaria,
        decimal valorApartamento,
        decimal? valorAmbulatorial = null)
    {
        return new TabelaPorteAnestesico
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperadoraId = operadoraId,
            PorteLetra = porteLetra,
            ValorEnfermaria = valorEnfermaria,
            ValorApartamento = valorApartamento,
            ValorAmbulatorial = valorAmbulatorial,
            AtualizadoEm = DateTimeOffset.UtcNow
        };
    }

    public void Atualizar(decimal valorEnfermaria, decimal valorApartamento, decimal? valorAmbulatorial = null)
    {
        ValorEnfermaria = valorEnfermaria;
        ValorApartamento = valorApartamento;
        ValorAmbulatorial = valorAmbulatorial;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }
}
