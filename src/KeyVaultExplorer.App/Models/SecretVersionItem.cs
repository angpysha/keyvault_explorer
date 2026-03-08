namespace KeyVaultExplorer.App.Models;

public sealed record SecretVersionItem(
    string Version,
    DateTimeOffset? UpdatedOn,
    bool Enabled);
