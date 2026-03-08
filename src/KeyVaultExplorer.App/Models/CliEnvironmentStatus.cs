namespace KeyVaultExplorer.App.Models;

public sealed record CliEnvironmentStatus(
    bool IsCliInstalled,
    bool IsLoggedIn,
    string? ActiveSubscriptionId,
    string Message);
