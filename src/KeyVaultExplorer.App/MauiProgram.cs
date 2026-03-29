using Microsoft.Extensions.Logging;
using KeyVaultExplorer.App.Services;
using KeyVaultExplorer.App.ViewModels;
using KeyVaultExplorer.App.Views;

namespace KeyVaultExplorer.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		AzureCliService.ConfigureCurrentProcessEnvironment();

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<AzureCliService>();
		builder.Services.AddSingleton<ExplorerState>();
		builder.Services.AddSingleton<KeyVaultService>();
		builder.Services.AddSingleton<AppShell>();

		builder.Services.AddTransient<SubscriptionsViewModel>();
		builder.Services.AddTransient<VaultsViewModel>();
		builder.Services.AddTransient<SecretsViewModel>();
		builder.Services.AddTransient<SecretDetailsViewModel>();

		builder.Services.AddTransient<SubscriptionsPage>();
		builder.Services.AddTransient<VaultsPage>();
		builder.Services.AddTransient<SecretsPage>();
		builder.Services.AddTransient<SecretDetailsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
