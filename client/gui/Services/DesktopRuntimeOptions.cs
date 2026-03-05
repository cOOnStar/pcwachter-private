namespace PCWachter.Desktop.Services;

public static class DesktopRuntimeOptions
{
    private const string MockOnlyEnvVar = "PCWACHTER_DESKTOP_MOCKUP_ONLY";

    public static bool ForceMockupOnly
    {
        get
        {
            string? raw = Environment.GetEnvironmentVariable(MockOnlyEnvVar);
            if (string.IsNullOrWhiteSpace(raw))
            {
                // Default for current UI iteration: design-first mockup mode.
                return true;
            }

            return !raw.Equals("0", StringComparison.OrdinalIgnoreCase)
                && !raw.Equals("false", StringComparison.OrdinalIgnoreCase)
                && !raw.Equals("off", StringComparison.OrdinalIgnoreCase)
                && !raw.Equals("no", StringComparison.OrdinalIgnoreCase);
        }
    }
}
