using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Commands;

/// <summary>
/// Command recording material texture slot assignments.
/// </summary>
public sealed class TextureAssignCommand : IEditorCommand
{
    private readonly MaterialData _material;
    private readonly string _slotName;
    private readonly TextureSlot? _oldSlot;
    private readonly TextureSlot? _newSlot;

    public string Name => $"Assign Texture to {_material.Name}[{_slotName}]";

    public TextureAssignCommand(MaterialData material, string slotName, TextureSlot? oldSlot, TextureSlot? newSlot)
    {
        _material = material;
        _slotName = slotName;
        _oldSlot = oldSlot?.DeepClone();
        _newSlot = newSlot?.DeepClone();
    }

    public void Execute()
    {
        if (_newSlot is null)
        {
            _material.TextureSlots.Remove(_slotName);
        }
        else
        {
            _material.TextureSlots[_slotName] = _newSlot.DeepClone();
        }
    }

    public void Undo()
    {
        if (_oldSlot is null)
        {
            _material.TextureSlots.Remove(_slotName);
        }
        else
        {
            _material.TextureSlots[_slotName] = _oldSlot.DeepClone();
        }
    }
}

/// <summary>
/// Command recording mesh UV transformations.
/// </summary>
public sealed class UVTransformCommand : IEditorCommand
{
    private readonly MeshData _mesh;
    private readonly List<Vector2D?> _oldUVs;
    private readonly List<Vector2D?> _newUVs;

    public string Name => $"Transform UVs on '{_mesh.Name}'";

    public UVTransformCommand(MeshData mesh, IEnumerable<Vector2D?> oldUVs, IEnumerable<Vector2D?> newUVs)
    {
        _mesh = mesh;
        _oldUVs = oldUVs.ToList();
        _newUVs = newUVs.ToList();
    }

    public void Execute()
    {
        for (int i = 0; i < _mesh.Vertices.Count && i < _newUVs.Count; i++)
        {
            _mesh.Vertices[i].TexCoord = _newUVs[i];
        }
    }

    public void Undo()
    {
        for (int i = 0; i < _mesh.Vertices.Count && i < _oldUVs.Count; i++)
        {
            _mesh.Vertices[i].TexCoord = _oldUVs[i];
        }
    }
}

/// <summary>
/// Composite command grouping multiple commands into a single atomic undo/redo action.
/// </summary>
public sealed class CompositeCommand : IEditorCommand
{
    private readonly List<IEditorCommand> _commands;

    public string Name { get; }
    public IReadOnlyList<IEditorCommand> Commands => _commands;

    public CompositeCommand(string name, IEnumerable<IEditorCommand> commands)
    {
        Name = name;
        _commands = commands.ToList();
    }

    public void Execute()
    {
        for (int i = 0; i < _commands.Count; i++)
        {
            _commands[i].Execute();
        }
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}

