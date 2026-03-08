using KeyVaultExplorer.App.Models;

namespace KeyVaultExplorer.App.Services;

public sealed class ExplorerState
{
    public SubscriptionInfo? SelectedSubscription { get; set; }

    public VaultInfo? SelectedVault { get; set; }

    public string? SelectedSecretName { get; set; }

    public string? SelectedSecretVersion { get; set; }

    public void ResetVaultContext()
    {
        SelectedVault = null;
        SelectedSecretName = null;
        SelectedSecretVersion = null;
    }

    public void ResetSecretContext()
    {
        SelectedSecretName = null;
        SelectedSecretVersion = null;
    }
}
