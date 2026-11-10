using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service inspecting an active USM scene against a target format to diagnose data approximations or loss.
/// </summary>
public interface IDataLossAnalyzer
{
    ConversionLossReport AnalyzeLoss(
        Scene3D scene,
        ModelFormatType targetFormat,
        ConversionOptions? options = null);
}
