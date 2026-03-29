# Key Vault Explorer

Key Vault Explorer is a .NET MAUI desktop app for browsing Azure Key Vault secrets on macOS and Windows.

The app uses your existing Azure CLI session via `az login` and `AzureCliCredential`, so there is no embedded sign-in flow and no client secret stored inside the app.

## Features

- Desktop-first UI built with .NET MAUI Shell for `Mac Catalyst` and `Windows`
- Subscription selection based on your Azure CLI account context
- Key Vault discovery for the selected subscription
- Secret list browsing with local search
- Secret version inspection
- Secret value preview with one-click clipboard copy
- Open-source friendly project layout and documentation

## Supported Platforms

- macOS through `Mac Catalyst`
- Windows through `WinUI`

## Prerequisites

- .NET 10 SDK
- .NET MAUI workload
- Azure CLI
- A valid Azure session created with `az login`
- Access to one or more Azure subscriptions and Key Vault resources

## Local Development

1. Install the required tooling.
2. Sign in with Azure CLI:

```bash
az login
```

3. Restore dependencies:

```bash
dotnet restore
```

4. Build for macOS:

```bash
dotnet build src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj -f net10.0-maccatalyst
```

5. Build for Windows from a Windows machine:

```powershell
dotnet build src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj -f net10.0-windows10.0.19041.0
```

6. Build a Windows MSI (WiX; run on Windows only):

```powershell
dotnet tool restore
.\scripts\Build-WindowsMsi.ps1
```

This publishes a self-contained `win-x64` Release build, harvests all output files into the installer (PDBs excluded), and produces `artifacts/windows/KeyVaultExplorer.msi`. The MSI is a dual-purpose package: the **Advanced** flow includes **install scope** (current user vs all users). Per-user installs typically land under `%LocalAppData%\Programs\`; per-machine installs under `Program Files\`. Silent installs can set scope via Windows Installer properties (for example `ALLUSERS` and `MSIINSTALLPERUSER`) as documented for dual-purpose packages. For a smaller MSI that requires the .NET desktop runtime on the machine, run `.\scripts\Build-WindowsMsi.ps1 -SelfContained:$false`.

7. Run the app from your target platform tooling or with `dotnet build` plus the normal MAUI launch workflow for that platform.

## How Authentication Works

- The app checks whether `az` is available in `PATH`.
- The app validates that an active Azure CLI session exists.
- Subscriptions and Key Vault discovery use Azure CLI commands.
- Secret listing, version loading, and value retrieval use the Azure SDK with `AzureCliCredential`.

This approach keeps the authentication model simple for engineers who already use Azure CLI locally.

## Project Structure

```text
src/KeyVaultExplorer.App/
  Models/
  Services/
  ViewModels/
  Views/
docs/wiki/
tests/KeyVaultExplorer.App.Tests/
```

## Screenshots

Screenshot placeholders can be added here after the first UI pass is finalized.

## Roadmap

- Add support for keys and certificates
- Add richer error states for RBAC and network issues
- Add Windows build validation in CI
- Add packaging and release notes automation

## Contributing

Contributions are welcome.

1. Open an issue describing the problem or idea.
2. Keep changes focused and easy to review.
3. Prefer small pull requests with screenshots for UI changes.
4. Make sure local build and tests pass before opening a PR.

## Wiki

- [Home](docs/wiki/Home.md)
- [Getting Started](docs/wiki/Getting-Started.md)
- [Architecture](docs/wiki/Architecture.md)

## License

Choose and add an open-source license before publishing the repository publicly.
