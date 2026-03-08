using KeyVaultExplorer.App.Services;

namespace KeyVaultExplorer.App.Tests;

public sealed class AzureCliJsonParserTests
{
    [Fact]
    public void ParseSubscriptions_SortsDefaultSubscriptionFirst()
    {
        var json = """
            [
              {
                "id": "sub-beta",
                "name": "Example Subscription Beta",
                "tenantId": "tenant-beta",
                "isDefault": false,
                "state": "Enabled",
                "user": { "name": "developer@example.test" }
              },
              {
                "id": "sub-alpha",
                "name": "Example Subscription Alpha",
                "tenantId": "tenant-alpha",
                "isDefault": true,
                "state": "Enabled",
                "user": { "name": "developer@example.test" }
              }
            ]
            """;

        var subscriptions = AzureCliJsonParser.ParseSubscriptions(json);

        Assert.Collection(
            subscriptions,
            first =>
            {
                Assert.Equal("sub-alpha", first.Id);
                Assert.True(first.IsDefault);
            },
            second => Assert.Equal("sub-beta", second.Id));
    }

    [Fact]
    public void ParseVaults_ReadsNestedVaultUri()
    {
        var json = """
            [
              {
                "name": "sample-vault",
                "resourceGroup": "sample-resource-group",
                "location": "sample-region",
                "properties": {
                  "vaultUri": "https://sample-vault.vault.azure.net/"
                }
              }
            ]
            """;

        var vaults = AzureCliJsonParser.ParseVaults(json);

        var vault = Assert.Single(vaults);
        Assert.Equal("sample-vault", vault.Name);
        Assert.Equal("sample-resource-group", vault.ResourceGroup);
        Assert.Equal("https://sample-vault.vault.azure.net/", vault.VaultUri);
    }

    [Fact]
    public void ParseVaults_BuildsVaultUriWhenCliOutputOmitsIt()
    {
        var json = """
            [
              {
                "name": "synthetic-vault",
                "resourceGroup": "synthetic-resource-group",
                "location": "synthetic-region",
                "type": "Microsoft.KeyVault/vaults"
              }
            ]
            """;

        var vaults = AzureCliJsonParser.ParseVaults(json);

        var vault = Assert.Single(vaults);
        Assert.Equal("https://synthetic-vault.vault.azure.net/", vault.VaultUri);
    }
}

public sealed class TextSearchTests
{
    [Theory]
    [InlineData("ConnectionString", "connection", true)]
    [InlineData("ConnectionString", "STRING", true)]
    [InlineData("ConnectionString", "token", false)]
    [InlineData("ConnectionString", "", true)]
    public void Matches_PerformsCaseInsensitiveContains(string input, string query, bool expected)
    {
        var result = TextSearch.Matches(input, query);

        Assert.Equal(expected, result);
    }
}
