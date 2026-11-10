using System.Text.RegularExpressions;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Plugin.SDK.Manifest;

namespace NEXUS3D.Plugin.SDK.Validation;

/// <summary>
/// Validates <see cref="PluginManifest"/> instances for schema correctness, security constraints, and semantic versioning.
/// </summary>
public static partial class PluginManifestValidator
{
    private static readonly Regex PluginIdRegex = GeneratedPluginIdRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled)]
    private static partial Regex GeneratedPluginIdRegex();

    public static ValidationResult Validate(PluginManifest? manifest)
    {
        var result = new ValidationResult();

        if (manifest is null)
        {
            result.AddError("MANIFEST_NULL", "Manifest cannot be null.");
            return result;
        }

        // 1. Plugin ID
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            result.AddError("MANIFEST_ID_EMPTY", "Plugin 'id' is required.", nameof(manifest.Id));
        }
        else if (manifest.Id.Length > 128)
        {
            result.AddError("MANIFEST_ID_TOO_LONG", "Plugin 'id' must not exceed 128 characters.", nameof(manifest.Id));
        }
        else if (!PluginIdRegex.IsMatch(manifest.Id))
        {
            result.AddError("MANIFEST_ID_INVALID", $"Plugin 'id' ('{manifest.Id}') contains invalid characters. Only alphanumeric, '.', '_', and '-' are allowed.", nameof(manifest.Id));
        }

        // 2. Name
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            result.AddError("MANIFEST_NAME_EMPTY", "Plugin 'name' is required.", nameof(manifest.Name));
        }
        else if (manifest.Name.Length > 256)
        {
            result.AddError("MANIFEST_NAME_TOO_LONG", "Plugin 'name' must not exceed 256 characters.", nameof(manifest.Name));
        }

        // 3. Version
        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            result.AddError("MANIFEST_VERSION_EMPTY", "Plugin 'version' is required.", nameof(manifest.Version));
        }
        else if (!Version.TryParse(manifest.Version, out _))
        {
            result.AddError("MANIFEST_VERSION_INVALID", $"Plugin 'version' ('{manifest.Version}') is not a valid semantic version.", nameof(manifest.Version));
        }

        // 4. Assembly & Path Traversal Prevention
        if (string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            result.AddError("MANIFEST_ASSEMBLY_EMPTY", "Plugin 'assembly' filename is required.", nameof(manifest.Assembly));
        }
        else
        {
            if (Path.IsPathRooted(manifest.Assembly))
            {
                result.AddError("MANIFEST_ASSEMBLY_ROOTED", "Plugin 'assembly' must be a relative file name, not an absolute path.", nameof(manifest.Assembly));
            }

            if (manifest.Assembly.Contains("..", StringComparison.Ordinal) || manifest.Assembly.Contains('/') || manifest.Assembly.Contains('\\'))
            {
                result.AddError("MANIFEST_ASSEMBLY_PATH_TRAVERSAL", "Plugin 'assembly' cannot contain path separators or directory traversal sequences.", nameof(manifest.Assembly));
            }

            if (!manifest.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("MANIFEST_ASSEMBLY_NOT_DLL", "Plugin 'assembly' must have a .dll extension.", nameof(manifest.Assembly));
            }
        }

        // 5. EntryPoint
        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
        {
            result.AddError("MANIFEST_ENTRYPOINT_EMPTY", "Plugin 'entryPoint' type name is required.", nameof(manifest.EntryPoint));
        }

        // 6. API Version
        if (string.IsNullOrWhiteSpace(manifest.PluginApiVersion))
        {
            result.AddError("MANIFEST_API_VERSION_EMPTY", "Plugin 'pluginApiVersion' is required.", nameof(manifest.PluginApiVersion));
        }
        else if (!Version.TryParse(manifest.PluginApiVersion, out _))
        {
            result.AddError("MANIFEST_API_VERSION_INVALID", $"Plugin 'pluginApiVersion' ('{manifest.PluginApiVersion}') is not valid.", nameof(manifest.PluginApiVersion));
        }

        // 7. Dependencies check
        if (manifest.Dependencies is { Count: > 0 })
        {
            for (int i = 0; i < manifest.Dependencies.Count; i++)
            {
                var dep = manifest.Dependencies[i];
                if (string.IsNullOrWhiteSpace(dep.PluginId))
                {
                    result.AddError("MANIFEST_DEP_ID_EMPTY", $"Dependency at index {i} has an empty 'pluginId'.", $"{nameof(manifest.Dependencies)}[{i}]");
                }
                else if (string.Equals(dep.PluginId, manifest.Id, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError("MANIFEST_SELF_DEPENDENCY", $"Plugin cannot declare a dependency on itself ('{dep.PluginId}').", $"{nameof(manifest.Dependencies)}[{i}]");
                }

                if (!string.IsNullOrWhiteSpace(dep.MinimumVersion) && !Version.TryParse(dep.MinimumVersion, out _))
                {
                    result.AddError("MANIFEST_DEP_VERSION_INVALID", $"Dependency '{dep.PluginId}' has an invalid 'minimumVersion' ('{dep.MinimumVersion}').", $"{nameof(manifest.Dependencies)}[{i}]");
                }
            }
        }

        return result;
    }
}

