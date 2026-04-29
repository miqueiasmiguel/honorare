using App;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Config;

public class ConfigurationValidatorTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=test",
            ["Google:ClientId"] = "client-id",
            ["Google:ClientSecret"] = "client-secret",
            ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-characters-ok",
            ["Jwt:Issuer"] = "https://test.com",
            ["Jwt:Audience"] = "test-audience",
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                data[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void Validate_AllRequiredKeysPresent_DoesNotThrow()
    {
        ConfigurationValidator.Validate(BuildConfig());
    }

    [Theory]
    [InlineData("ConnectionStrings:Default")]
    [InlineData("Google:ClientId")]
    [InlineData("Google:ClientSecret")]
    [InlineData("Jwt:Secret")]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public void Validate_MissingKey_ThrowsWithKeyInMessage(string missingKey)
    {
        var config = BuildConfig(new Dictionary<string, string?> { [missingKey] = null });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigurationValidator.Validate(config));

        Assert.Contains(missingKey, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ConnectionStrings:Default", "   ")]
    [InlineData("Google:ClientId", "")]
    [InlineData("Google:ClientSecret", " ")]
    [InlineData("Jwt:Secret", "\t")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    public void Validate_WhitespaceValue_ThrowsWithKeyInMessage(string key, string whitespace)
    {
        var config = BuildConfig(new Dictionary<string, string?> { [key] = whitespace });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigurationValidator.Validate(config));

        Assert.Contains(key, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_JwtSecretShorterThan32Chars_ThrowsWithKeyInMessage()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["Jwt:Secret"] = "too-short" });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigurationValidator.Validate(config));

        Assert.Contains("Jwt:Secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_JwtSecretExactly32Chars_DoesNotThrow()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["Jwt:Secret"] = new string('x', 32) });

        ConfigurationValidator.Validate(config);
    }

    [Fact]
    public void Validate_MultipleMissingKeys_ListsAllInSingleMessage()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = null,
            ["Google:ClientSecret"] = null,
            ["Jwt:Issuer"] = "",
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigurationValidator.Validate(config));

        Assert.Contains("Google:ClientId", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Google:ClientSecret", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Jwt:Issuer", ex.Message, StringComparison.Ordinal);
    }
}
