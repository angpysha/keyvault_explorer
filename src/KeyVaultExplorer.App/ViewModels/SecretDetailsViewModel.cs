using System.Collections.ObjectModel;
using System.Windows.Input;
using KeyVaultExplorer.App.Models;
using KeyVaultExplorer.App.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace KeyVaultExplorer.App.ViewModels;

public sealed class SecretDetailsViewModel : ViewModelBase
{
    private readonly ExplorerState _explorerState;
    private readonly KeyVaultService _keyVaultService;
    private bool _hasLoaded;
    private bool _isApplyingSelection;
    private string? _loadedSecretName;
    private string? _loadedVaultUri;
    private string? _secretValue;
    private string? _contentType;
    private string? _updatedOnText;
    private string? _tagsText;
    private bool _isSecretEnabled;
    private SecretVersionItem? _selectedVersion;

    public SecretDetailsViewModel(KeyVaultService keyVaultService, ExplorerState explorerState)
    {
        _keyVaultService = keyVaultService;
        _explorerState = explorerState;

        RefreshCommand = new Command(async () => await LoadAsync(forceRefresh: true));
        CopyValueCommand = new Command(async () => await CopyValueAsync(), () => CanCopyValue);

        PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(SecretValue) or nameof(IsBusy))
            {
                OnPropertyChanged(nameof(CanCopyValue));
                (CopyValueCommand as Command)?.ChangeCanExecute();
            }
        };
    }

    public ObservableCollection<SecretVersionItem> Versions { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand CopyValueCommand { get; }

    public string HeaderTitle =>
        string.IsNullOrWhiteSpace(_explorerState.SelectedSecretName)
            ? "No secret selected"
            : _explorerState.SelectedSecretName;

    public string VaultSubtitle =>
        _explorerState.SelectedVault is null
            ? "Choose a Key Vault first."
            : $"Vault: {_explorerState.SelectedVault.Name}";

    public SecretVersionItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (SetProperty(ref _selectedVersion, value) && !_isApplyingSelection)
            {
                _explorerState.SelectedSecretVersion = value?.Version;
                _ = LoadSelectedVersionAsync(value?.Version);
            }
        }
    }

    public string? SecretValue
    {
        get => _secretValue;
        private set => SetProperty(ref _secretValue, value);
    }

    public string? ContentType
    {
        get => _contentType;
        private set => SetProperty(ref _contentType, value);
    }

    public string? UpdatedOnText
    {
        get => _updatedOnText;
        private set => SetProperty(ref _updatedOnText, value);
    }

    public string? TagsText
    {
        get => _tagsText;
        private set => SetProperty(ref _tagsText, value);
    }

    public bool IsSecretEnabled
    {
        get => _isSecretEnabled;
        private set => SetProperty(ref _isSecretEnabled, value);
    }

    public bool CanCopyValue => !string.IsNullOrWhiteSpace(SecretValue) && !IsBusy;

    public Task InitializeAsync()
    {
        var currentVaultUri = _explorerState.SelectedVault?.VaultUri;
        var currentSecretName = _explorerState.SelectedSecretName;

        if (_hasLoaded && string.Equals(currentVaultUri, _loadedVaultUri, StringComparison.OrdinalIgnoreCase)
            && string.Equals(currentSecretName, _loadedSecretName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return LoadAsync(forceRefresh: true);
    }

    private Task LoadAsync(bool forceRefresh)
    {
        if (_hasLoaded && !forceRefresh)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            var vault = _explorerState.SelectedVault;
            var secretName = _explorerState.SelectedSecretName;

            if (vault is null || string.IsNullOrWhiteSpace(secretName))
            {
                SetError("Choose a secret first.");
                return;
            }

            var versions = await _keyVaultService.GetSecretVersionsAsync(vault.VaultUri, secretName);
            Versions.Clear();
            foreach (var version in versions)
            {
                Versions.Add(version);
            }

            _isApplyingSelection = true;
            SelectedVersion =
                Versions.FirstOrDefault(version => version.Version == _explorerState.SelectedSecretVersion)
                ?? Versions.FirstOrDefault();
            _isApplyingSelection = false;

            _loadedVaultUri = vault.VaultUri;
            _loadedSecretName = secretName;
            _hasLoaded = true;

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(VaultSubtitle));

            if (SelectedVersion is not null)
            {
                await LoadSelectedVersionCoreAsync(SelectedVersion.Version);
            }
            else
            {
                SecretValue = null;
                ContentType = null;
                UpdatedOnText = null;
                TagsText = null;
                IsSecretEnabled = false;
                SetStatus("This secret does not have version metadata.");
            }
        }, "Loading secret versions...");
    }

    private Task LoadSelectedVersionAsync(string? version)
    {
        var vault = _explorerState.SelectedVault;
        var secretName = _explorerState.SelectedSecretName;

        if (vault is null || string.IsNullOrWhiteSpace(secretName))
        {
            SetError("Choose a secret first.");
            return Task.CompletedTask;
        }

        return RunBusyAsync(() => LoadSelectedVersionCoreAsync(version), "Loading secret value...");
    }

    private async Task LoadSelectedVersionCoreAsync(string? version)
    {
        var vault = _explorerState.SelectedVault;
        var secretName = _explorerState.SelectedSecretName;

        if (vault is null || string.IsNullOrWhiteSpace(secretName))
        {
            SetError("Choose a secret first.");
            return;
        }

        var details = await _keyVaultService.GetSecretAsync(vault.VaultUri, secretName, version);
        SecretValue = details.Value;
        ContentType = string.IsNullOrWhiteSpace(details.ContentType) ? "n/a" : details.ContentType;
        UpdatedOnText = details.UpdatedOn?.ToLocalTime().ToString("u") ?? "n/a";
        IsSecretEnabled = details.Enabled;
        TagsText = details.Tags.Count == 0
            ? "No tags"
            : string.Join(Environment.NewLine, details.Tags.Select(tag => $"{tag.Key}: {tag.Value}"));
        SetStatus($"Loaded secret version {details.Version ?? "latest"}.");
    }

    private Task CopyValueAsync()
    {
        if (string.IsNullOrWhiteSpace(SecretValue))
        {
            SetError("There is no secret value to copy.");
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            await Clipboard.Default.SetTextAsync(SecretValue);
            SetStatus("Secret value copied to the clipboard.");
        }, "Copying secret value...");
    }
}
