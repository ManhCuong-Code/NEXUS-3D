using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Engine.Commands;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class UndoRedoServiceTests
{
    [Fact]
    public void Execute_PushesToUndo_ClearsRedo()
    {
        var undoRedo = new UndoRedoService();
        var node = new SceneNode("TestNode") { LocalTransform = Transform3D.Identity() };

        var newTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, Vector3D.One);
        var command = new TransformCommand("Move", node, node.LocalTransform, newTransform);

        Assert.False(undoRedo.CanUndo);
        Assert.False(undoRedo.CanRedo);

        undoRedo.Execute(command);

        Assert.True(undoRedo.CanUndo);
        Assert.False(undoRedo.CanRedo);
        Assert.Equal(new Vector3D(10, 0, 0), node.LocalTransform.Position);

        // Undo
        undoRedo.Undo();
        Assert.False(undoRedo.CanUndo);
        Assert.True(undoRedo.CanRedo);
        Assert.Equal(Vector3D.Zero, node.LocalTransform.Position);

        // Redo
        undoRedo.Redo();
        Assert.True(undoRedo.CanUndo);
        Assert.False(undoRedo.CanRedo);
        Assert.Equal(new Vector3D(10, 0, 0), node.LocalTransform.Position);
    }

    [Fact]
    public void CapacityLimit_PrunesOldestCommands()
    {
        var undoRedo = new UndoRedoService(maxHistory: 10);
        var node = new SceneNode("Node");

        for (int i = 0; i < 25; i++)
        {
            var cmd = new TransformCommand($"Move {i}", node, Transform3D.Identity(), new Transform3D(new Vector3D(i, 0, 0), QuaternionD.Identity, Vector3D.One));
            undoRedo.Execute(cmd);
        }

        Assert.Equal(10, undoRedo.UndoHistory.Count);
        Assert.Equal("Move 24", undoRedo.UndoHistory.Last().Name);
    }
}

