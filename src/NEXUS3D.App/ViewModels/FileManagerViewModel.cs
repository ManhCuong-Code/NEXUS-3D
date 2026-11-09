using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel for the File Manager &amp; History module.
/// Manages recent files, pinned assets, and conversion history logs.
/// </summary>
public sealed partial class FileManagerViewModel : BaseViewModel
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IFormatRegistry _formatRegistry;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private RecentFileEntry? _selectedRecentFile;

    [ObservableProperty]
    private ConversionHistoryRecord? _selectedHistoryRecord;

    public ObservableCollection<RecentFileEntry> RecentFiles { get; } = [];
    public ObservableCollection<ConversionHistoryRecord> ConversionHistory { get; } = [];

    private List<RecentFileEntry> _allRecentFiles = [];
    private List<ConversionHistoryRecord> _allHistory = [];

    public FileManagerViewModel(
        IFileManagerService fileManagerService,
        IFormatRegistry formatRegistry,
        INotificationService notificationService)
    {
        _fileManagerService = Guard.NotNull(fileManagerService);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _notificationService = Guard.NotNull(notificationService);

        Title = "File Manager & History";
        Subtitle = "Manage recent 3D model files, pinned workspace assets, and conversion audit logs.";
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken = default) =>
        LoadAllRecordsAsync(cancellationToken);

    partial void OnSearchFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAllRecordsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var filesResult = await _fileManagerService.GetRecentFilesAsync(50, cancellationToken);
            var historyResult = await _fileManagerService.GetConversionHistoryAsync(100, cancellationToken);

            if (filesResult.IsSuccess && filesResult.Value is not null)
            {
                _allRecentFiles = filesResult.Value.ToList();
            }

            if (historyResult.IsSuccess && historyResult.Value is not null)
            {
                _allHistory = historyResult.Value.ToList();
            }

            ApplyFilter();
            StatusMessage = $"Loaded {_allRecentFiles.Count} recent files and {_allHistory.Count} history records.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddModelFileToWorkspaceAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Register 3D Model File in Workspace",
            Filter = _formatRegistry.BuildFileDialogFilter(includeAllSupported: true),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            var result = await _fileManagerService.RegisterFileAccessAsync(dialog.FileName);
            if (result.IsSuccess)
            {
                _notificationService.Publish(
                    "Model File Registered",
                    $"Added '{Path.GetFileName(dialog.FileName)}' to Recent Files.",
                    NotificationSeverity.Success,
                    "FileManager");
                await LoadAllRecordsAsync();
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
    }

    [RelayCommand]
    private async Task TogglePinAsync(RecentFileEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var result = await _fileManagerService.TogglePinRecentFileAsync(entry);
        if (result.IsSuccess)
        {
            await LoadAllRecordsAsync();
        }
    }

    [RelayCommand]
    private async Task RemoveRecentFileAsync(RecentFileEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var result = await _fileManagerService.RemoveRecentFileAsync(entry.Id);
        if (result.IsSuccess)
        {
            await LoadAllRecordsAsync();
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var result = await _fileManagerService.ClearHistoryAsync();
        if (result.IsSuccess)
        {
            _notificationService.Publish(
                "History Cleared",
                "Cleared conversion history and unpinned recent files.",
                NotificationSeverity.Info,
                "FileManager");
            await LoadAllRecordsAsync();
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }

    private void ApplyFilter()
    {
        string term = SearchFilter?.Trim() ?? string.Empty;

        RecentFiles.Clear();
        foreach (var file in _allRecentFiles)
        {
            if (string.IsNullOrEmpty(term) ||
                file.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                file.FilePath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                file.Format.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                RecentFiles.Add(file);
            }
        }

        ConversionHistory.Clear();
        foreach (var item in _allHistory)
        {
            if (string.IsNullOrEmpty(term) ||
                item.JobName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.SourceFilePath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.TargetFilePath.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                ConversionHistory.Add(item);
            }
        }
    }
}
