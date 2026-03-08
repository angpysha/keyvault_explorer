namespace KeyVaultExplorer.App.Services;

public static class TextSearch
{
    public static bool Matches(string? input, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(input)
            && input.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
