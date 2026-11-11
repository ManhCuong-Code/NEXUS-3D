namespace NEXUS3D.Shared.Constants;

/// <summary>
/// Application-wide constants for NEXUS 3D.
/// Uses relative folder names so paths are resolved dynamically via Environment.SpecialFolder.
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "NEXUS-3D";
    public const string ApplicationSubtitle = "Universal 3D & Schematic Converter";
    public const string ApplicationCodeName = "NEXUS3D";
    public const string Version = "1.0.0";
    public const string StageName = "Official Release";
    public const string DefaultDatabaseFileName = "nexus3d.sqlite";
    public const string DefaultConfigFileName = "nexus3d.settings.json";
    public const string LogsFolderName = "Logs";
    public const string CacheFolderName = "Cache";
    public const string PluginsFolderName = "Plugins";
    public const string ProfilesFolderName = "Profiles";
    public const int CurrentSchemaVersion = 1;
}

