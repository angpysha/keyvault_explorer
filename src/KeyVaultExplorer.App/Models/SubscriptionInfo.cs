namespace KeyVaultExplorer.App.Models;

public sealed record SubscriptionInfo(
    string Id,
    string Name,
    string TenantId,
    bool IsDefault,
    string State,
    string UserName);
