using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Shared.Constants;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Infrastructure.Windows;

/// <summary>
/// Production Windows Desktop Shortcut service leveraging native Windows Shell COM (IShellLinkW / IPersistFile)
/// with fallback to Windows Script Host (WScript.Shell).
/// Resolves user Desktop folder dynamically, validates target executable and icon existence,
/// prevents unwanted creation in temporary development build directories, and supports unicode (Vietnamese) and space-containing paths.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DesktopShortcutService : IDesktopShortcutService
{
    private const string DefaultShortcutFileName = "NEXUS-3D.lnk";
    private const string WScriptShellProgId = "WScript.Shell";

    private readonly ILogger<DesktopShortcutService> _logger;
    private readonly string? _targetExeOverride;
    private readonly string? _desktopDirOverride;
    private readonly string? _iconPathOverride;

    public DesktopShortcutService(
        ILogger<DesktopShortcutService> logger,
        string? targetExeOverride = null,
        string? desktopDirOverride = null,
        string? iconPathOverride = null)
    {
        _logger = logger;
        _targetExeOverride = targetExeOverride;
        _desktopDirOverride = desktopDirOverride;
        _iconPathOverride = iconPathOverride;
    }

    /// <summary>
    /// Checks whether the desktop shortcut exists on the current user's desktop.
    /// </summary>
    public bool CheckShortcutExists()
    {
        try
        {
            string shortcutPath = GetShortcutFilePath();
            return File.Exists(shortcutPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check desktop shortcut existence.");
            return false;
        }
    }

    /// <summary>
    /// Returns comprehensive metadata and status for the NEXUS-3D desktop shortcut.
    /// </summary>
    public DesktopShortcutStatus GetShortcutStatus()
    {
        string shortcutPath = GetShortcutFilePath();
        string targetExe = GetCurrentExecutablePath();
        string workingDir = GetInstallationDirectory();
        string iconLoc = LocateIconPath(workingDir);
        bool isDev = IsInDevelopmentBuildDirectory(targetExe);
        bool exists = File.Exists(shortcutPath);

        if (!exists)
        {
            return new DesktopShortcutStatus(
                Exists: false,
                ShortcutPath: shortcutPath,
                TargetPath: targetExe,
                WorkingDirectory: workingDir,
                IconLocation: iconLoc,
                IsTargetValid: File.Exists(targetExe),
                IsDevelopmentBuild: isDev,
                Message: "Shortcut is not present on the Windows Desktop.");
        }

        try
        {
            // Primary reader: IShellLinkW (Unicode safe)
            var (scTarget, scWorkDir, scIcon) = ReadShortcutViaShellLink(shortcutPath);

            if (string.IsNullOrWhiteSpace(scTarget))
            {
                // Fallback reader: WScript.Shell
                var wshStatus = ReadShortcutViaWsh(shortcutPath);
                scTarget = wshStatus.target;
                scWorkDir = wshStatus.workDir;
                scIcon = wshStatus.icon;
            }

            bool isTargetValid = !string.IsNullOrWhiteSpace(scTarget) && File.Exists(scTarget);

            return new DesktopShortcutStatus(
                Exists: true,
                ShortcutPath: shortcutPath,
                TargetPath: string.IsNullOrWhiteSpace(scTarget) ? targetExe : scTarget,
                WorkingDirectory: string.IsNullOrWhiteSpace(scWorkDir) ? workingDir : scWorkDir,
                IconLocation: string.IsNullOrWhiteSpace(scIcon) ? iconLoc : scIcon,
                IsTargetValid: isTargetValid,
                IsDevelopmentBuild: isDev,
                Message: isTargetValid ? "Shortcut is active and pointing to a valid executable." : "Shortcut target executable does not exist on disk.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect desktop shortcut at '{ShortcutPath}'.", shortcutPath);
            return new DesktopShortcutStatus(
                Exists: true,
                ShortcutPath: shortcutPath,
                TargetPath: targetExe,
                WorkingDirectory: workingDir,
                IconLocation: iconLoc,
                IsTargetValid: File.Exists(targetExe),
                IsDevelopmentBuild: isDev,
                Message: $"Shortcut exists but failed to read metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the Windows Desktop shortcut pointing to the application executable.
    /// </summary>
    public OperationResult CreateShortcut(bool overwrite = false)
    {
        try
        {
            string shortcutPath = GetShortcutFilePath();
            string targetExe = GetCurrentExecutablePath();
            string workingDir = GetInstallationDirectory();

            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            {
                _logger.LogError("Cannot create shortcut: executable target file does not exist at '{TargetExe}'.", targetExe);
                return OperationResult.Failure("EXE_NOT_FOUND", $"Executable target file does not exist at '{targetExe}'.");
            }

            if (File.Exists(shortcutPath) && !overwrite)
            {
                _logger.LogInformation("Shortcut already exists on desktop at '{ShortcutPath}'.", shortcutPath);
                return OperationResult.Failure("SHORTCUT_EXISTS", $"Shortcut already exists on Desktop at '{shortcutPath}'. Use Recreate to update.");
            }

            string? desktopDir = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrWhiteSpace(desktopDir) && !Directory.Exists(desktopDir))
            {
                Directory.CreateDirectory(desktopDir);
            }

            string iconLocation = LocateIconPath(workingDir);
            string description = $"{AppConstants.ApplicationName} – {AppConstants.ApplicationSubtitle}";

            // Primary: IShellLinkW (Full Unicode / UTF-16 support for Vietnamese and all international paths)
            try
            {
                CreateShortcutViaShellLink(shortcutPath, targetExe, workingDir, iconLocation, description);
                _logger.LogInformation("Successfully created desktop shortcut via IShellLinkW at '{ShortcutPath}' -> '{TargetExe}'.", shortcutPath, targetExe);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IShellLinkW creation failed, trying fallback to WScript.Shell COM.");
            }

            // Fallback: WScript.Shell COM automation
            CreateShortcutViaWsh(shortcutPath, targetExe, workingDir, iconLocation, description);
            _logger.LogInformation("Successfully created desktop shortcut via WScript.Shell at '{ShortcutPath}' -> '{TargetExe}'.", shortcutPath, targetExe);

            return OperationResult.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while creating desktop shortcut.");
            return OperationResult.Failure("ACCESS_DENIED", $"Access denied while writing shortcut: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating desktop shortcut.");
            return OperationResult.Failure("SHORTCUT_CREATE_ERR", $"Failed to create desktop shortcut: {ex.Message}");
        }
    }

    /// <summary>
    /// Recreates the desktop shortcut, updating target and icon.
    /// </summary>
    public OperationResult RecreateShortcut() => CreateShortcut(overwrite: true);

    /// <summary>
    /// Removes the desktop shortcut if it exists.
    /// </summary>
    public OperationResult RemoveShortcut()
    {
        try
        {
            string shortcutPath = GetShortcutFilePath();
            if (!File.Exists(shortcutPath))
            {
                _logger.LogInformation("Shortcut does not exist at '{ShortcutPath}'. No removal required.", shortcutPath);
                return OperationResult.Success();
            }

            File.Delete(shortcutPath);
            _logger.LogInformation("Successfully removed desktop shortcut at '{ShortcutPath}'.", shortcutPath);
            return OperationResult.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while removing desktop shortcut.");
            return OperationResult.Failure("ACCESS_DENIED", $"Access denied while deleting shortcut: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error removing desktop shortcut.");
            return OperationResult.Failure("SHORTCUT_DELETE_ERR", $"Failed to remove desktop shortcut: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the application installation directory in Windows Explorer.
    /// </summary>
    public OperationResult OpenInstallationFolder()
    {
        try
        {
            string workingDir = GetInstallationDirectory();
            if (!Directory.Exists(workingDir))
            {
                return OperationResult.Failure("DIR_NOT_FOUND", $"Installation directory does not exist: '{workingDir}'");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = workingDir,
                UseShellExecute = true
            });

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open installation folder.");
            return OperationResult.Failure("EXPLORER_LAUNCH_ERR", $"Failed to launch Windows Explorer: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the shortcut exists if requested by user preferences, but skips development build folders.
    /// </summary>
    public OperationResult EnsureShortcutOnStartup(bool autoCreateRequested)
    {
        if (!autoCreateRequested)
        {
            return OperationResult.Success();
        }

        string targetExe = GetCurrentExecutablePath();
        if (IsInDevelopmentBuildDirectory(targetExe))
        {
            _logger.LogInformation("Skipping automatic desktop shortcut creation because current process is running in development build folder: '{TargetExe}'.", targetExe);
            return OperationResult.Success();
        }

        if (CheckShortcutExists())
        {
            return OperationResult.Success();
        }

        return CreateShortcut(overwrite: false);
    }

    /// <summary>
    /// Resolves the desktop directory for the current user.
    /// </summary>
    public string GetDesktopDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_desktopDirOverride))
        {
            return Path.GetFullPath(_desktopDirOverride);
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
        {
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        if (string.IsNullOrWhiteSpace(desktop))
        {
            desktop = AppContext.BaseDirectory;
        }

        return desktop;
    }

    /// <summary>
    /// Resolves the full path to NEXUS-3D.lnk on the desktop.
    /// </summary>
    public string GetShortcutFilePath() => Path.Combine(GetDesktopDirectory(), DefaultShortcutFileName);

    /// <summary>
    /// Resolves the real target executable path.
    /// </summary>
    public string GetCurrentExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_targetExeOverride))
        {
            return Path.GetFullPath(_targetExeOverride);
        }

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            string fileName = Path.GetFileName(processPath);
            // If running via dotnet test host or testhost.exe, find NEXUS3D.App.exe
            if (fileName.Equals("testhost.exe", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(AppContext.BaseDirectory, "NEXUS3D.App.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.GetFullPath(processPath);
        }

        string fallback = Path.Combine(AppContext.BaseDirectory, "NEXUS3D.App.exe");
        if (File.Exists(fallback))
        {
            return fallback;
        }

        return Path.Combine(AppContext.BaseDirectory, "NEXUS-3D.exe");
    }

    /// <summary>
    /// Resolves the working directory containing the executable.
    /// </summary>
    public string GetInstallationDirectory()
    {
        string exePath = GetCurrentExecutablePath();
        string? dir = Path.GetDirectoryName(exePath);
        return !string.IsNullOrWhiteSpace(dir) ? dir : AppContext.BaseDirectory;
    }

    /// <summary>
    /// Locates the official application .ico file, or falls back to the embedded executable icon.
    /// </summary>
    public string LocateIconPath(string appDir)
    {
        if (!string.IsNullOrWhiteSpace(_iconPathOverride) && File.Exists(_iconPathOverride))
        {
            return Path.GetFullPath(_iconPathOverride);
        }

        // Candidate 1: appDir/Resources/app.ico
        string candidate1 = Path.Combine(appDir, "Resources", "app.ico");
        if (File.Exists(candidate1))
        {
            return candidate1;
        }

        // Candidate 2: appDir/app.ico
        string candidate2 = Path.Combine(appDir, "app.ico");
        if (File.Exists(candidate2))
        {
            return candidate2;
        }

        // Candidate 3: Scan up parents to find repository assets/branding/app.ico or src/NEXUS3D.App/Resources/app.ico
        try
        {
            var dir = new DirectoryInfo(appDir);
            while (dir != null && dir.Exists)
            {
                string repoIcon = Path.Combine(dir.FullName, "assets", "branding", "app.ico");
                if (File.Exists(repoIcon))
                {
                    return repoIcon;
                }

                string appIcon = Path.Combine(dir.FullName, "src", "NEXUS3D.App", "Resources", "app.ico");
                if (File.Exists(appIcon))
                {
                    return appIcon;
                }

                dir = dir.Parent;
            }
        }
        catch { }

        // Fallback: Icon index 0 embedded in executable
        return $"{GetCurrentExecutablePath()},0";
    }

    /// <summary>
    /// Detects whether the executable resides in a temporary development build directory (bin\Debug, bin\Release, obj).
    /// </summary>
    public bool IsInDevelopmentBuildDirectory(string? exePath = null)
    {
        string path = exePath ?? GetCurrentExecutablePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('/', '\\');
        return normalized.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase);
    }

    #region Native Windows Shell COM (IShellLinkW)

    private static void CreateShortcutViaShellLink(
        string shortcutPath,
        string targetExe,
        string workingDir,
        string iconLocation,
        string description)
    {
        var link = (IShellLinkW)new ShellLinkObject();
        link.SetPath(targetExe);
        link.SetWorkingDirectory(workingDir);
        link.SetArguments(string.Empty);
        link.SetDescription(description);
        link.SetShowCmd(1); // SW_SHOWNORMAL

        if (!string.IsNullOrWhiteSpace(iconLocation))
        {
            int commaIdx = iconLocation.LastIndexOf(',');
            if (commaIdx > 0 && int.TryParse(iconLocation[(commaIdx + 1)..].Trim(), out int iconIdx))
            {
                link.SetIconLocation(iconLocation[..commaIdx].Trim(), iconIdx);
            }
            else
            {
                link.SetIconLocation(iconLocation, 0);
            }
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, true);
    }

    private static (string Target, string WorkDir, string Icon) ReadShortcutViaShellLink(string shortcutPath)
    {
        var link = (IShellLinkW)new ShellLinkObject();
        var file = (IPersistFile)link;
        file.Load(shortcutPath, 0); // STGM_READ

        var sbTarget = new StringBuilder(1024);
        var pfd = new WIN32_FIND_DATAW();
        link.GetPath(sbTarget, sbTarget.Capacity, ref pfd, 0);

        var sbWorkDir = new StringBuilder(1024);
        link.GetWorkingDirectory(sbWorkDir, sbWorkDir.Capacity);

        var sbIcon = new StringBuilder(1024);
        link.GetIconLocation(sbIcon, sbIcon.Capacity, out int iconIndex);

        string icon = sbIcon.Length > 0 ? $"{sbIcon},{iconIndex}" : string.Empty;
        return (sbTarget.ToString(), sbWorkDir.ToString(), icon);
    }

    private static void CreateShortcutViaWsh(
        string shortcutPath,
        string targetExe,
        string workingDir,
        string iconLocation,
        string description)
    {
        Type? shellType = Type.GetTypeFromProgID(WScriptShellProgId);
        if (shellType is null)
        {
            throw new InvalidOperationException("WScript.Shell COM type is not available.");
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);

        shortcut.TargetPath = targetExe;
        shortcut.WorkingDirectory = workingDir;
        shortcut.Arguments = string.Empty;
        shortcut.WindowStyle = 1;
        shortcut.Description = description;

        if (!string.IsNullOrWhiteSpace(iconLocation))
        {
            shortcut.IconLocation = iconLocation.Contains(',') ? iconLocation : $"{iconLocation},0";
        }

        shortcut.Save();
    }

    private static (string target, string workDir, string icon) ReadShortcutViaWsh(string shortcutPath)
    {
        Type? shellType = Type.GetTypeFromProgID(WScriptShellProgId);
        if (shellType is null) return (string.Empty, string.Empty, string.Empty);

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        return ((string)shortcut.TargetPath, (string)shortcut.WorkingDirectory, (string)shortcut.IconLocation);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private class ShellLinkObject { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    #endregion
}
