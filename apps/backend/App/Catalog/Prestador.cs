using App.Identity;

namespace App.Catalog;

internal sealed class Prestador : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? RegistroProfissional { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public string? EmailAcesso { get; private set; }

    private Prestador() { }

    public static Prestador Create(Guid tenantId, string nome, string? registroProfissional)
    {
        return new Prestador
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome,
            RegistroProfissional = registroProfissional,
            Ativo = true,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    public void Atualizar(string nome, string? registroProfissional, bool ativo)
    {
        Nome = nome;
        RegistroProfissional = registroProfissional;
        Ativo = ativo;
    }

    internal Result SetEmailAcesso(string email)
    {
        if (EmailAcesso is not null)
        {
            return Result.Fail(new ConflictError("E-mail de acesso já definido para este prestador."));
        }

#pragma warning disable CA1308 // e-mail deve ser armazenado em minúsculas por convenção
        EmailAcesso = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        return Result.Ok();
    }
}
