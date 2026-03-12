using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using KeyVaultExplorer.App.Models;

namespace KeyVaultExplorer.App.Services;

public sealed class AzureCliService
{
    private static readonly string[] CandidateExecutablePaths =
    [
        "az",
        "/opt/homebrew/bin/az",
        "/usr/local/bin/az",
        "/usr/bin/az",
        "az.cmd",
        "az.exe"
    ];

    public async Task<CliEnvironmentStatus> GetEnvironmentStatusAsync(CancellationToken cancellationToken = default)
    {
        var versionResult = await RunAzCommandAsync("--version", cancellationToken);
        if (!versionResult.IsSuccess)
        {
            return new CliEnvironmentStatus(
                IsCliInstalled: false,
                IsLoggedIn: false,
                ActiveSubscriptionId: null,
                Message: BuildCliMissingMessage(versionResult));
        }

        var accountResult = await RunAzCommandAsync("account show --output json", cancellationToken);
        if (!accountResult.IsSuccess)
        {
            return new CliEnvironmentStatus(
                IsCliInstalled: true,
                IsLoggedIn: false,
                ActiveSubscriptionId: null,
                Message: "Azure CLI is installed, but there is no active session. Run az login in a terminal.");
        }

        var subscriptions = AzureCliJsonParser.ParseSubscriptions($"[{accountResult.StandardOutput}]");
        var activeSubscriptionId = subscriptions.FirstOrDefault()?.Id;

        return new CliEnvironmentStatus(
            IsCliInstalled: true,
            IsLoggedIn: true,
            ActiveSubscriptionId: activeSubscriptionId,
            Message: "Azure CLI session detected. Choose a subscription to continue.");
    }

    public async Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAzCommandAsync("account list --all --output json", cancellationToken);
        EnsureSuccess(result, "Unable to load subscriptions from Azure CLI.");
        return AzureCliJsonParser.ParseSubscriptions(result.StandardOutput);
    }

    public async Task SetActiveSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var result = await RunAzCommandAsync($"account set --subscription \"{subscriptionId}\"", cancellationToken);
        EnsureSuccess(result, "Unable to switch the active subscription.");
    }

    public async Task<IReadOnlyList<VaultInfo>> GetVaultsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var result = await RunAzCommandAsync(
            $"keyvault list --subscription \"{subscriptionId}\" --output json",
            cancellationToken);

        EnsureSuccess(result, "Unable to load Key Vault resources for the selected subscription.");
        return AzureCliJsonParser.ParseVaults(result.StandardOutput);
    }

    private static void EnsureSuccess(CommandResult result, string message)
    {
        if (result.IsSuccess)
        {
            return;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
            ? message
            : $"{message} {result.StandardError}".Trim());
    }

    private static async Task<CommandResult> RunAzCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        var executablePath = ResolveAzureCliPath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new CommandResult(false, string.Empty, "Azure CLI executable was not found.");
        }

        var directResult = await RunProcessAsync(
            CreateDirectStartInfo(executablePath, arguments),
            cancellationToken);

        if (directResult.IsSuccess)
        {
            return directResult;
        }

        if (!OperatingSystem.IsWindows())
        {
            var shellFallbackResult = await RunProcessAsync(
                CreateShellStartInfo(executablePath, arguments),
                cancellationToken);

            if (shellFallbackResult.IsSuccess)
            {
                return shellFallbackResult;
            }

            return new CommandResult(
                false,
                string.Empty,
                $"Direct launch failed: {directResult.StandardError} Shell fallback failed: {shellFallbackResult.StandardError}".Trim());
        }

        return directResult;
    }

    private static ProcessStartInfo CreateDirectStartInfo(string executablePath, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo CreateShellStartInfo(string executablePath, string arguments)
    {
        var escapedExecutablePath = executablePath.Replace("\"", "\\\"");
        var command = $"\"{escapedExecutablePath}\" {arguments}";

        return new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-lc \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task<CommandResult> RunProcessAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.StartInfo.Environment["PATH"] = BuildProcessPathEnvironment();

            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();

            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    standardOutput.AppendLine(eventArgs.Data);
                }
            };

            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    standardError.AppendLine(eventArgs.Data);
                }
            };

            if (!process.Start())
            {
                return new CommandResult(false, string.Empty, "Unable to start Azure CLI process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult(
                process.ExitCode == 0,
                standardOutput.ToString().Trim(),
                standardError.ToString().Trim());
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new CommandResult(false, string.Empty, exception.Message);
        }
    }

    private static string BuildCliMissingMessage(CommandResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StandardError))
        {
            return "Azure CLI was not found in PATH. Install Azure CLI and run az login.";
        }

        return $"Azure CLI is unavailable for this app build. {result.StandardError}".Trim();
    }

    private static string? ResolveAzureCliPath()
    {
        foreach (var candidate in CandidateExecutablePaths)
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                continue;
            }

            var resolved = ResolveFromPath(candidate);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveFromPath(string executableName)
    {
        var pathValue = BuildProcessPathEnvironment();
        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedSegment = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmedSegment))
            {
                continue;
            }

            var fullPath = Path.Combine(trimmedSegment, executableName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string BuildProcessPathEnvironment()
    {
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var segments = new List<string>();

        foreach (var segment in currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            AddPathSegment(segments, ExpandHomeDirectory(segment));
        }

        AddPathSegment(segments, "/opt/homebrew/bin");
        AddPathSegment(segments, "/opt/homebrew/sbin");
        AddPathSegment(segments, "/usr/local/bin");
        AddPathSegment(segments, "/usr/bin");
        AddPathSegment(segments, "/bin");
        AddPathSegment(segments, "/usr/sbin");
        AddPathSegment(segments, "/sbin");

        return string.Join(Path.PathSeparator, segments);
    }

    private static void AddPathSegment(List<string> segments, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!segments.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            segments.Add(path);
        }
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (!path.StartsWith("~/", StringComparison.Ordinal))
        {
            return path;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, path[2..]);
    }

    private sealed record CommandResult(bool IsSuccess, string StandardOutput, string StandardError);
}
