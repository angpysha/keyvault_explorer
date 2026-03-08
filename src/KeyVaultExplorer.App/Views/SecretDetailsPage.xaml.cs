using KeyVaultExplorer.App.ViewModels;

namespace KeyVaultExplorer.App.Views;

public partial class SecretDetailsPage : ContentPage
{
    private readonly SecretDetailsViewModel _viewModel;

    public SecretDetailsPage(SecretDetailsViewModel viewModel)
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
