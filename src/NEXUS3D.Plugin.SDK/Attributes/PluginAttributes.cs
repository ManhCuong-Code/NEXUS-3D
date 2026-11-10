namespace NEXUS3D.Plugin.SDK.Attributes;

/// <summary>
/// Decorates an entry point class implementing <see cref="Contracts.INexusPlugin"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NexusPluginAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }

    public NexusPluginAttribute(string id, string name, string version = "1.0.0")
    {
        Id = id;
        Name = name;
        Version = version;
    }
}

/// <summary>
/// Decorates a format descriptor or parser declaring supported file extensions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SupportedFormatAttribute : Attribute
{
    public string FormatName { get; }
    public string PrimaryExtension { get; }
    public string ShortCode { get; }

    public SupportedFormatAttribute(string formatName, string primaryExtension, string shortCode)
    {
        FormatName = formatName;
        PrimaryExtension = primaryExtension;
        ShortCode = shortCode;
    }
}

