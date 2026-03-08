using KeyVaultExplorer.App.ViewModels;

namespace KeyVaultExplorer.App.Views;

public partial class VaultsPage : ContentPage
{
    private readonly VaultsViewModel _viewModel;

    public VaultsPage(VaultsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
