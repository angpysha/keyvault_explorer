namespace KeyVaultExplorer.App.Models;

public sealed record SecretValueDetails(
    string Name,
    string? Version,
    string? Value,
    string? ContentType,
    DateTimeOffset? UpdatedOn,
    bool Enabled,
    IReadOnlyDictionary<string, string> Tags);
