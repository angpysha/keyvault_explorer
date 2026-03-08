namespace KeyVaultExplorer.App.Models;

public sealed record VaultInfo(
    string Name,
    string VaultUri,
    string ResourceGroup,
    string Location);
