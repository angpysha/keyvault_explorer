using System.Collections.ObjectModel;
using System.Windows.Input;
using KeyVaultExplorer.App.Models;
using KeyVaultExplorer.App.Services;
using KeyVaultExplorer.App.Views;

namespace KeyVaultExplorer.App.ViewModels;

public sealed class SecretsViewModel : ViewModelBase
{
    private readonly ExplorerState _explorerState;
    private readonly KeyVaultService _keyVaultService;
    private readonly List<SecretItem> _allSecrets = [];
    private bool _hasLoaded;
    private string? _searchText;

    public SecretsViewModel(KeyVaultService keyVaultService, ExplorerState explorerState)
    {
        _keyVaultService = keyVaultService;
        _explorerState = explorerState;

        RefreshCommand = new Command(async () => await LoadAsync(forceRefresh: true));
        OpenSecretCommand = new Command<SecretItem>(async secret => await OpenSecretAsync(secret));
    }

    public ObservableCollection<SecretItem> Secrets { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand OpenSecretCommand { get; }

    public string HeaderTitle =>
        _explorerState.SelectedVault is null
            ? "No Key Vault selected"
            : $"Secrets in {_explorerState.SelectedVault.Name}";

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public Task InitializeAsync()
    {
        return _hasLoaded ? Task.CompletedTask : LoadAsync(forceRefresh: false);
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
            if (vault is null)
            {
                SetError("Choose a Key Vault first.");
                return;
            }

            var secrets = await _keyVaultService.GetSecretsAsync(vault.VaultUri);
            _allSecrets.Clear();
            _allSecrets.AddRange(secrets);
            ApplyFilter();
            _hasLoaded = true;
            OnPropertyChanged(nameof(HeaderTitle));
            SetStatus($"Loaded {_allSecrets.Count} secret entries.");
        }, "Loading Key Vault secrets...");
    }

    private Task OpenSecretAsync(SecretItem? secret)
    {
        if (secret is null)
        {
            return Task.CompletedTask;
        }

        _explorerState.SelectedSecretName = secret.Name;
        _explorerState.SelectedSecretVersion = null;

        return Shell.Current is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync($"//{nameof(SecretDetailsPage)}");
    }

    private void ApplyFilter()
    {
        var filteredSecrets = _allSecrets
            .Where(secret => TextSearch.Matches(secret.Name, SearchText))
            .ToList();

        Secrets.Clear();
        foreach (var secret in filteredSecrets)
        {
            Secrets.Add(secret);
        }
    }
}
