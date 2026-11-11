using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Infrastructure.Windows;
using Xunit;

namespace NEXUS3D.Integration.Tests;

/// <summary>
/// Comprehensive integration tests for Windows Desktop Shortcut creation, inspection, and removal.
/// Runs in isolated temporary directories without affecting the host user's actual desktop.
/// </summary>
public sealed class DesktopShortcutServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _mockDesktopDir;
    private readonly string _mockAppDir;
    private readonly string _mockExePath;
    private readonly string _mockIconPath;
    private readonly DesktopShortcutService _service;

    public DesktopShortcutServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "nexus3d_sc_test_" + Guid.NewGuid().ToString("N"));
        _mockDesktopDir = Path.Combine(_testRoot, "Desktop");
        _mockAppDir = Path.Combine(_testRoot, "ProgramFiles", "NEXUS-3D", "Application");

        Directory.CreateDirectory(_mockDesktopDir);
        Directory.CreateDirectory(_mockAppDir);

        _mockExePath = Path.Combine(_mockAppDir, "NEXUS3D.App.exe");
        File.WriteAllText(_mockExePath, "MOCK_EXE_BINARY_DATA");

        _mockIconPath = Path.Combine(_mockAppDir, "app.ico");
        File.WriteAllText(_mockIconPath, "MOCK_ICO_BINARY_DATA");

        _service = new DesktopShortcutService(
            NullLogger<DesktopShortcutService>.Instance,
            targetExeOverride: _mockExePath,
            desktopDirOverride: _mockDesktopDir,
            iconPathOverride: _mockIconPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void GetShortcutStatus_WhenNotExist_ReportsNotExists()
    {
        var status = _service.GetShortcutStatus();

        Assert.False(status.Exists);
        Assert.False(_service.CheckShortcutExists());
        Assert.Equal(_mockExePath, status.TargetPath);
        Assert.Equal(_mockAppDir, status.WorkingDirectory);
        Assert.Equal(_mockIconPath, status.IconLocation);
        Assert.True(status.IsTargetValid);
    }

    [Fact]
    public void CreateShortcut_InIsolatedDirectory_SuccessfullyCreatesLnkFile()
    {
        var result = _service.CreateShortcut(overwrite: false);

        Assert.True(result.IsSuccess);
        Assert.True(_service.CheckShortcutExists());

        var status = _service.GetShortcutStatus();
        Assert.True(status.Exists);
        Assert.True(status.IsTargetValid);
        Assert.Equal(_mockExePath, Path.GetFullPath(status.TargetPath));
        Assert.Equal(_mockAppDir, Path.GetFullPath(status.WorkingDirectory));
    }

    [Fact]
    public void CreateShortcut_WhenAlreadyExists_FailsWithoutOverwrite()
    {
        var firstResult = _service.CreateShortcut(overwrite: false);
        Assert.True(firstResult.IsSuccess);

        var secondResult = _service.CreateShortcut(overwrite: false);
        Assert.False(secondResult.IsSuccess);
        Assert.Equal("SHORTCUT_EXISTS", secondResult.ErrorCode);
    }

    [Fact]
    public void RecreateShortcut_WhenAlreadyExists_OverwritesSuccessfully()
    {
        var firstResult = _service.CreateShortcut(overwrite: false);
        Assert.True(firstResult.IsSuccess);

        var recreateResult = _service.RecreateShortcut();
        Assert.True(recreateResult.IsSuccess);
        Assert.True(_service.CheckShortcutExists());
    }

    [Fact]
    public void RemoveShortcut_DeletesLnkFileFromDesktop()
    {
        _service.CreateShortcut(overwrite: false);
        Assert.True(_service.CheckShortcutExists());

        var removeResult = _service.RemoveShortcut();
        Assert.True(removeResult.IsSuccess);
        Assert.False(_service.CheckShortcutExists());

        // Removing non-existent shortcut should succeed gracefully
        var secondRemove = _service.RemoveShortcut();
        Assert.True(secondRemove.IsSuccess);
    }

    [Fact]
    public void CreateShortcut_WithSpacesAndVietnameseCharacters_Succeeds()
    {
        string unicodeAppDir = Path.Combine(_testRoot, "Thư Mục Cài Đặt Tiếng Việt", "NEXUS 3D");
        Directory.CreateDirectory(unicodeAppDir);

        string unicodeExe = Path.Combine(unicodeAppDir, "NEXUS 3D Converter.exe");
        File.WriteAllText(unicodeExe, "EXE");

        string unicodeIco = Path.Combine(unicodeAppDir, "Biểu Tượng.ico");
        File.WriteAllText(unicodeIco, "ICO");

        var unicodeService = new DesktopShortcutService(
            NullLogger<DesktopShortcutService>.Instance,
            targetExeOverride: unicodeExe,
            desktopDirOverride: _mockDesktopDir,
            iconPathOverride: unicodeIco);

        var result = unicodeService.CreateShortcut(overwrite: true);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        var status = unicodeService.GetShortcutStatus();
        Assert.True(status.Exists);
        Assert.True(status.IsTargetValid);
        Assert.Equal(Path.GetFullPath(unicodeExe), Path.GetFullPath(status.TargetPath));
    }

    [Fact]
    public void EnsureShortcutOnStartup_WhenInDevelopmentBuild_SkipsAutoCreation()
    {
        string devExePath = Path.Combine(_testRoot, "src", "NEXUS3D.App", "bin", "Debug", "net10.0-windows", "NEXUS3D.App.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(devExePath)!);
        File.WriteAllText(devExePath, "DEV_EXE");

        var devService = new DesktopShortcutService(
            NullLogger<DesktopShortcutService>.Instance,
            targetExeOverride: devExePath,
            desktopDirOverride: _mockDesktopDir);

        Assert.True(devService.IsInDevelopmentBuildDirectory());

        var result = devService.EnsureShortcutOnStartup(autoCreateRequested: true);
        Assert.True(result.IsSuccess);
        Assert.False(devService.CheckShortcutExists()); // Must skip creation for dev builds
    }

    [Fact]
    public void EnsureShortcutOnStartup_WhenInReleaseInstall_CreatesShortcut()
    {
        Assert.False(_service.IsInDevelopmentBuildDirectory());

        var result = _service.EnsureShortcutOnStartup(autoCreateRequested: true);
        Assert.True(result.IsSuccess);
        Assert.True(_service.CheckShortcutExists());
    }

    [Fact]
    public void CreateShortcut_WhenTargetExeMissing_FailsGracefully()
    {
        string nonExistentExe = Path.Combine(_testRoot, "missing_folder", "does_not_exist.exe");

        var brokenService = new DesktopShortcutService(
            NullLogger<DesktopShortcutService>.Instance,
            targetExeOverride: nonExistentExe,
            desktopDirOverride: _mockDesktopDir);

        var result = brokenService.CreateShortcut();
        Assert.False(result.IsSuccess);
        Assert.Equal("EXE_NOT_FOUND", result.ErrorCode);
    }
}

