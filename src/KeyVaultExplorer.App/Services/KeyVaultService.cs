using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using KeyVaultExplorer.App.Models;

namespace KeyVaultExplorer.App.Services;

public sealed class KeyVaultService
{
    private readonly SecretClientOptions _clientOptions = new()
    {
        Retry =
        {
            Delay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(8),
            MaxRetries = 3,
            Mode = RetryMode.Exponential
        }
    };

    public async Task<IReadOnlyList<SecretItem>> GetSecretsAsync(string vaultUri, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(vaultUri);
        var items = new List<SecretItem>();

        await foreach (var secretProperties in client.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            items.Add(new SecretItem(
                Name: secretProperties.Name,
                UpdatedOn: secretProperties.UpdatedOn,
                Enabled: secretProperties.Enabled ?? true));
        }

        return items
            .OrderBy(secret => secret.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<SecretVersionItem>> GetSecretVersionsAsync(
        string vaultUri,
        string secretName,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient(vaultUri);
        var items = new List<SecretVersionItem>();

        await foreach (var secretProperties in client.GetPropertiesOfSecretVersionsAsync(secretName, cancellationToken))
        {
            items.Add(new SecretVersionItem(
                Version: secretProperties.Version,
                UpdatedOn: secretProperties.UpdatedOn,
                Enabled: secretProperties.Enabled ?? true));
        }

        return items
            .OrderByDescending(version => version.UpdatedOn)
            .ToList();
    }

    public async Task<SecretValueDetails> GetSecretAsync(
        string vaultUri,
        string secretName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient(vaultUri);
        KeyVaultSecret secret;

        try
        {
            secret = await client.GetSecretAsync(secretName, version, cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new InvalidOperationException(
                $"Unable to load the secret value for '{secretName}'. {exception.Message}",
                exception);
        }

        return new SecretValueDetails(
            Name: secret.Name,
            Version: secret.Properties.Version,
            Value: secret.Value,
            ContentType: secret.Properties.ContentType,
            UpdatedOn: secret.Properties.UpdatedOn,
            Enabled: secret.Properties.Enabled ?? true,
            Tags: new Dictionary<string, string>(secret.Properties.Tags, StringComparer.OrdinalIgnoreCase));
    }

    private SecretClient CreateClient(string vaultUri)
    {
        return new SecretClient(new Uri(vaultUri), new AzureCliCredential(), _clientOptions);
    }
}
