using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

internal static class HextechBehindCreaturesLayer
{
	private const string NodeName = "HextechRunes_BehindCreatures";

	internal static Node2D? GetOrCreate(Node? renderParent)
	{
		if (!GodotObject.IsInstanceValid(renderParent))
		{
			return null;
		}

		Node2D? layer = renderParent.GetNodeOrNull<Node2D>(NodeName);
		if (!GodotObject.IsInstanceValid(layer))
		{
			layer = new Node2D
			{
				Name = NodeName,
				ShowBehindParent = false,
				TopLevel = false,
				ZAsRelative = true,
				ZIndex = 0
			};
			renderParent.AddChildSafely(layer);
		}

		EnsureRenderOrder(layer);
		return layer;
	}

	internal static void EnsureRenderOrder(Node2D? layer)
	{
		if (!GodotObject.IsInstanceValid(layer)
			|| layer.GetParent() is not Node renderParent
			|| !GodotObject.IsInstanceValid(renderParent)
			|| layer.GetIndex() == 0)
		{
			return;
		}

		renderParent.MoveChildSafely(layer, 0);
	}
}
