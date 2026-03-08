namespace KeyVaultExplorer.App.Models;

public sealed record SecretItem(
    string Name,
    DateTimeOffset? UpdatedOn,
    bool Enabled);
