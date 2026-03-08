using KeyVaultExplorer.App.ViewModels;

namespace KeyVaultExplorer.App.Views;

public partial class SecretsPage : ContentPage
{
    private readonly SecretsViewModel _viewModel;

    public SecretsPage(SecretsViewModel viewModel)
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
