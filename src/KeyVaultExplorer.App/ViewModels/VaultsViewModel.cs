using System.Collections.ObjectModel;
using System.Windows.Input;
using KeyVaultExplorer.App.Models;
using KeyVaultExplorer.App.Services;
using KeyVaultExplorer.App.Views;

namespace KeyVaultExplorer.App.ViewModels;

public sealed class VaultsViewModel : ViewModelBase
{
    private readonly AzureCliService _azureCliService;
    private readonly ExplorerState _explorerState;
    private readonly List<VaultInfo> _allVaults = [];
    private bool _hasLoaded;
    private string? _searchText;

    public VaultsViewModel(AzureCliService azureCliService, ExplorerState explorerState)
    {
        _azureCliService = azureCliService;
        _explorerState = explorerState;

        RefreshCommand = new Command(async () => await LoadAsync(forceRefresh: true));
        OpenVaultCommand = new Command<VaultInfo>(async vault => await OpenVaultAsync(vault));
    }

    public ObservableCollection<VaultInfo> Vaults { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand OpenVaultCommand { get; }

    public string HeaderTitle =>
        _explorerState.SelectedSubscription is null
            ? "No subscription selected"
            : $"Key Vault resources in {_explorerState.SelectedSubscription.Name}";

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
            var subscription = _explorerState.SelectedSubscription;
            if (subscription is null)
            {
                SetError("Choose a subscription first.");
                return;
            }

            var vaults = await _azureCliService.GetVaultsAsync(subscription.Id);
            _allVaults.Clear();
            _allVaults.AddRange(vaults);
            ApplyFilter();
            _hasLoaded = true;
            OnPropertyChanged(nameof(HeaderTitle));
            SetStatus($"Loaded {_allVaults.Count} Key Vault resources.");
        }, "Loading Key Vault resources...");
    }

    private Task OpenVaultAsync(VaultInfo? vault)
    {
        if (vault is null)
        {
            return Task.CompletedTask;
        }

        _explorerState.SelectedVault = vault;
        _explorerState.ResetSecretContext();

        return Shell.Current is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync($"//{nameof(SecretsPage)}");
    }

    private void ApplyFilter()
    {
        var filteredVaults = _allVaults
            .Where(vault =>
                TextSearch.Matches(vault.Name, SearchText)
                || TextSearch.Matches(vault.Location, SearchText)
                || TextSearch.Matches(vault.ResourceGroup, SearchText))
            .ToList();

        Vaults.Clear();
        foreach (var vault in filteredVaults)
        {
            Vaults.Add(vault);
        }
    }
}
