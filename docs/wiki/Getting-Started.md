# Getting Started

## Requirements

- .NET 10 SDK
- .NET MAUI workload
- Azure CLI
- Access to Azure subscriptions and Key Vault resources

## Login

Authenticate with Azure CLI before opening the app:

```bash
az login
```

If you use multiple subscriptions, the app will let you choose the active one after startup.

## Build

### macOS

```bash
dotnet build src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj -f net10.0-maccatalyst
```

### Windows

```powershell
dotnet build src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj -f net10.0-windows10.0.19041.0
```

## First Run Flow

1. Start the app.
2. Confirm that Azure CLI is installed and that `az login` is active.
3. Select the subscription you want to work with.
4. Open a Key Vault from the vault list.
5. Search for a secret by name if needed.
6. Open the secret details page.
7. Select a version and copy the value if required.

## Common Problems

### Azure CLI not found

Install Azure CLI and verify that `az` is available in your shell.

### No active login session

Run:

```bash
az login
```

### No Key Vault resources or secrets visible

Check:

- Your active subscription
- RBAC or access policy permissions
- Whether the Key Vault contains secrets
