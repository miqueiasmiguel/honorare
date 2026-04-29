namespace App;

internal static class ConfigurationValidator
{
    private record RequiredKey(string Key, int? MinLength = null);

    private static readonly RequiredKey[] _keys =
    [
        new("ConnectionStrings:Default"),
        new("Google:ClientId"),
        new("Google:ClientSecret"),
        new("Jwt:Secret", MinLength: 32),
        new("Jwt:Issuer"),
        new("Jwt:Audience"),
    ];

    internal static void Validate(IConfiguration configuration)
    {
        var errors = new List<string>();

        foreach (var required in _keys)
        {
            var value = configuration[required.Key];

            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"'{required.Key}' está ausente ou vazio");
                continue;
            }

            if (required.MinLength.HasValue && value.Length < required.MinLength.Value)
            {
                errors.Add($"'{required.Key}' deve ter pelo menos {required.MinLength.Value} caracteres (atual: {value.Length})");
            }
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "A aplicação não pode iniciar. Configurações obrigatórias ausentes ou inválidas:\n" +
            string.Join("\n", errors.Select(e => $"  • {e}")));
    }
}
