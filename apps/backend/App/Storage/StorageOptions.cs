namespace App.Storage;

internal sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string BasePath { get; set; } = string.Empty;
}
