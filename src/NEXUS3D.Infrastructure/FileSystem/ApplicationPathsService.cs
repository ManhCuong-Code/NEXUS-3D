using NEXUS3D.Application.Interfaces;
using NEXUS3D.Shared.Constants;

namespace NEXUS3D.Infrastructure.FileSystem;

/// <summary>
/// Resolves application directories dynamically without hard-coding drive letters.
/// Supports custom root directory override for isolated testing.
/// </summary>
public sealed class ApplicationPathsService : IApplicationPathsService
{
    public string AppDataRootDirectory { get; }
    public string DatabaseFilePath { get; }
    public string ConfigFilePath { get; }
    public string LogsDirectory { get; }
    public string CacheDirectory { get; }
    public string PluginsDirectory { get; }
    public string ProfilesDirectory { get; }

    public ApplicationPathsService(string? customRootDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(customRootDirectory))
        {
            AppDataRootDirectory = Path.GetFullPath(customRootDirectory);
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = AppContext.BaseDirectory;
            }

            AppDataRootDirectory = Path.Combine(localAppData, AppConstants.ApplicationCodeName);
        }

        DatabaseFilePath = Path.Combine(AppDataRootDirectory, AppConstants.DefaultDatabaseFileName);
        ConfigFilePath = Path.Combine(AppDataRootDirectory, AppConstants.DefaultConfigFileName);
        LogsDirectory = Path.Combine(AppDataRootDirectory, AppConstants.LogsFolderName);
        CacheDirectory = Path.Combine(AppDataRootDirectory, AppConstants.CacheFolderName);
        PluginsDirectory = Path.Combine(AppDataRootDirectory, AppConstants.PluginsFolderName);
        ProfilesDirectory = Path.Combine(AppDataRootDirectory, AppConstants.ProfilesFolderName);
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataRootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(Path.Combine(AppDataRootDirectory, "Exports"));
    }
}

