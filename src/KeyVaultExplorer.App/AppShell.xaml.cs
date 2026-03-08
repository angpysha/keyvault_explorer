using KeyVaultExplorer.App.Views;

namespace KeyVaultExplorer.App;

public partial class AppShell : Shell
{
	public AppShell(IServiceProvider serviceProvider)
	{
		InitializeComponent();

		Items.Add(CreateShellContent("Subscriptions", nameof(SubscriptionsPage), () => serviceProvider.GetRequiredService<SubscriptionsPage>()));
		Items.Add(CreateShellContent("Vaults", nameof(VaultsPage), () => serviceProvider.GetRequiredService<VaultsPage>()));
		Items.Add(CreateShellContent("Secrets", nameof(SecretsPage), () => serviceProvider.GetRequiredService<SecretsPage>()));
		Items.Add(CreateShellContent("Secret Details", nameof(SecretDetailsPage), () => serviceProvider.GetRequiredService<SecretDetailsPage>()));
	}

	private static ShellContent CreateShellContent(string title, string route, Func<Page> pageFactory)
	{
		return new ShellContent
		{
			Title = title,
			Route = route,
			ContentTemplate = new DataTemplate(pageFactory)
		};
	}
}
