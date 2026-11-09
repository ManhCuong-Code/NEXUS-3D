using CommunityToolkit.Mvvm.ComponentModel;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Base class for all WPF ViewModels in NEXUS 3D using CommunityToolkit.Mvvm.
/// Provides common busy state, status text, error banner state, and async lifecycle hooks.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Called when the view associated with this ViewModel is navigated to.
    /// </summary>
    public virtual Task OnNavigatedToAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    protected void ClearError() => ErrorMessage = null;
}

