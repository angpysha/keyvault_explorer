using System.Text.Json;
using KeyVaultExplorer.App.Models;

namespace KeyVaultExplorer.App.Services;

public static class AzureCliJsonParser
{
    public static IReadOnlyList<SubscriptionInfo> ParseSubscriptions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = new List<SubscriptionInfo>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            items.Add(new SubscriptionInfo(
                Id: GetString(element, "id"),
                Name: GetString(element, "name"),
                TenantId: GetString(element, "tenantId"),
                IsDefault: element.TryGetProperty("isDefault", out var isDefaultProperty) && isDefaultProperty.GetBoolean(),
                State: GetString(element, "state"),
                UserName: TryGetNestedString(element, "user", "name")));
        }

        return items
            .OrderByDescending(subscription => subscription.IsDefault)
            .ThenBy(subscription => subscription.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<VaultInfo> ParseVaults(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = new List<VaultInfo>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var properties = element.TryGetProperty("properties", out var propertiesElement)
                ? propertiesElement
                : default;

            items.Add(new VaultInfo(
                Name: GetString(element, "name"),
                VaultUri: GetVaultUri(element, properties),
                ResourceGroup: GetString(element, "resourceGroup"),
                Location: GetString(element, "location")));
        }

        return items
            .OrderBy(vault => vault.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetVaultUri(JsonElement element, JsonElement properties)
    {
        var vaultUri = GetString(properties, "vaultUri");
        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            return vaultUri;
        }

        var vaultName = GetString(element, "name");
        if (string.IsNullOrWhiteSpace(vaultName))
        {
            return string.Empty;
        }

        // Azure CLI list output does not always include properties.vaultUri,
        // but the public vault endpoint is deterministic for Azure public cloud.
        return $"https://{vaultName}.vault.azure.net/";
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    private static string TryGetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || !property.TryGetProperty(nestedPropertyName, out var nestedProperty))
        {
            return string.Empty;
        }

        return nestedProperty.GetString() ?? string.Empty;
    }
}
