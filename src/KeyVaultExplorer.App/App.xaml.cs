namespace KeyVaultExplorer.App;

public partial class App : Application
{
	private readonly AppShell _appShell;

	public App(AppShell appShell)
	{
		_appShell = appShell;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell)
		{
			Title = "Key Vault Explorer"
		};
	}
}