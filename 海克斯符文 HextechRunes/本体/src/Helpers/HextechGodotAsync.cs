using Godot;

namespace HextechRunes;

internal static class HextechGodotAsync
{
	internal static async Task<bool> AwaitProcessFrameAsync(Node node)
	{
		if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
		{
			return false;
		}

		SceneTree tree = node.GetTree();
		if (tree == null)
		{
			return false;
		}

		await node.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		return GodotObject.IsInstanceValid(node) && node.IsInsideTree();
	}
}
