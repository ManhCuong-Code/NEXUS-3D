namespace NEXUS3D.PluginSystem.Versioning;

/// <summary>
/// Enforces semantic version compatibility between plugins and the NEXUS-3D Host Application.
/// </summary>
public static class PluginApiVersionChecker
{
    public static readonly Version CurrentHostApiVersion = new(1, 0, 0);
    public static readonly Version CurrentHostVersion = new(1, 0, 0);

    /// <summary>
    /// Checks whether the specified plugin manifest is compatible with the running host environment.
    /// </summary>
    public static bool IsCompatible(
        string pluginApiVersionStr,
        string minimumHostVersionStr,
        out string? failureReason)
    {
        failureReason = null;

        if (!Version.TryParse(pluginApiVersionStr, out var pluginApiVersion))
        {
            failureReason = $"Invalid plugin API version format: '{pluginApiVersionStr}'.";
            return false;
        }

        if (pluginApiVersion.Major != CurrentHostApiVersion.Major)
        {
            failureReason = $"Incompatible API Major Version. Plugin requires API v{pluginApiVersion.Major}.x, but host provides v{CurrentHostApiVersion.Major}.x.";
            return false;
        }

        if (pluginApiVersion.Minor > CurrentHostApiVersion.Minor)
        {
            failureReason = $"Plugin requires newer API v{pluginApiVersion}, but current host API is v{CurrentHostApiVersion}.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(minimumHostVersionStr) && Version.TryParse(minimumHostVersionStr, out var minHostVersion))
        {
            if (CurrentHostVersion < minHostVersion)
            {
                failureReason = $"Host version v{CurrentHostVersion} is below minimum required version v{minHostVersion}.";
                return false;
            }
        }

        return true;
    }
}

