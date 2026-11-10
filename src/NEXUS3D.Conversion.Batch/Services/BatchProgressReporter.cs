using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;

namespace NEXUS3D.Conversion.Batch.Services;

/// <summary>
/// Dispatches batch conversion progress updates to event listeners and UI observers.
/// </summary>
public sealed class BatchProgressReporter : IBatchProgressReporter
{
    public event EventHandler<BatchProgress>? ProgressChanged;

    public void Report(BatchProgress progress)
    {
        ProgressChanged?.Invoke(this, progress);
    }
}

