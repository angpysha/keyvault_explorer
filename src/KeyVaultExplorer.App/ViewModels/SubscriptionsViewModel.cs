using System.Collections.ObjectModel;
using System.Windows.Input;
using KeyVaultExplorer.App.Models;
using KeyVaultExplorer.App.Services;
using KeyVaultExplorer.App.Views;

namespace KeyVaultExplorer.App.ViewModels;

public sealed class SubscriptionsViewModel : ViewModelBase
{
    private readonly AzureCliService _azureCliService;
    private readonly ExplorerState _explorerState;
    private readonly List<SubscriptionInfo> _allSubscriptions = [];
    private bool _hasLoaded;
    private string? _searchText;
    private SubscriptionInfo? _selectedSubscription;

    public SubscriptionsViewModel(AzureCliService azureCliService, ExplorerState explorerState)
    {
        _azureCliService = azureCliService;
        _explorerState = explorerState;

        RefreshCommand = new Command(async () => await LoadAsync(forceRefresh: true));
        OpenVaultsCommand = new Command(async () => await OpenVaultsAsync(), () => CanContinue);
        PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(IsBusy))
            {
                OnPropertyChanged(nameof(CanContinue));
                (OpenVaultsCommand as Command)?.ChangeCanExecute();
            }
        };
    }

    public ObservableCollection<SubscriptionInfo> Subscriptions { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand OpenVaultsCommand { get; }

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

    public SubscriptionInfo? SelectedSubscription
    {
        get => _selectedSubscription;
        set
        {
            if (SetProperty(ref _selectedSubscription, value))
            {
                OnPropertyChanged(nameof(CanContinue));
                (OpenVaultsCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool CanContinue => SelectedSubscription is not null && !IsBusy;

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
            var status = await _azureCliService.GetEnvironmentStatusAsync();
            if (!status.IsCliInstalled || !status.IsLoggedIn)
            {
                _allSubscriptions.Clear();
                Subscriptions.Clear();
                SelectedSubscription = null;
                _hasLoaded = true;
                SetError(status.Message);
                return;
            }

            var subscriptions = await _azureCliService.GetSubscriptionsAsync();
            _allSubscriptions.Clear();
            _allSubscriptions.AddRange(subscriptions);
            ApplyFilter();

            SelectedSubscription =
                _allSubscriptions.FirstOrDefault(subscription => subscription.Id == _explorerState.SelectedSubscription?.Id)
                ?? _allSubscriptions.FirstOrDefault(subscription => subscription.Id == status.ActiveSubscriptionId)
                ?? _allSubscriptions.FirstOrDefault();

            _hasLoaded = true;
            SetStatus($"Loaded {_allSubscriptions.Count} subscriptions from Azure CLI.");
        }, "Checking Azure CLI session...");
    }

    private Task OpenVaultsAsync()
    {
        if (SelectedSubscription is null)
        {
            SetError("Choose a subscription before opening Key Vault resources.");
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            await _azureCliService.SetActiveSubscriptionAsync(SelectedSubscription.Id);
            _explorerState.SelectedSubscription = SelectedSubscription;
            _explorerState.ResetVaultContext();
            SetStatus($"Active subscription: {SelectedSubscription.Name}");

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"//{nameof(VaultsPage)}");
            }
        }, "Switching Azure subscription...");
    }

    private void ApplyFilter()
    {
        var filteredSubscriptions = _allSubscriptions
            .Where(subscription =>
                TextSearch.Matches(subscription.Name, SearchText)
                || TextSearch.Matches(subscription.Id, SearchText)
                || TextSearch.Matches(subscription.TenantId, SearchText)
                || TextSearch.Matches(subscription.UserName, SearchText))
            .ToList();

        Subscriptions.Clear();
        foreach (var subscription in filteredSubscriptions)
        {
            Subscriptions.Add(subscription);
        }
    }
}
