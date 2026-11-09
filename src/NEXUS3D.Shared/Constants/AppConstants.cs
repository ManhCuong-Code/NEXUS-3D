namespace NEXUS3D.Shared.Constants;

/// <summary>
/// Application-wide constants for NEXUS 3D.
/// Uses relative folder names so paths are resolved dynamically via Environment.SpecialFolder.
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "NEXUS 3D";
    public const string ApplicationSubtitle = "Universal 3D Model Converter & Editor";
    public const string ApplicationCodeName = "NEXUS3D";
    public const string Version = "0.1.0";
    public const string StageName = "Phase 01 – Foundation";
    public const string DefaultDatabaseFileName = "nexus3d.sqlite";
    public const string DefaultConfigFileName = "nexus3d.settings.json";
    public const string LogsFolderName = "Logs";
    public const string CacheFolderName = "Cache";
    public const string PluginsFolderName = "Plugins";
    public const string ProfilesFolderName = "Profiles";
    public const int CurrentSchemaVersion = 1;
}

