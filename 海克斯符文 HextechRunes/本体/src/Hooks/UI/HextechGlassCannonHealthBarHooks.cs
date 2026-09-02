using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal sealed class HextechGlassCannonHealthBarVisual
{
	private const string OverlayName = "HextechRunes_GlassCannonLock";
	private static readonly Color DimColor = new(0.05f, 0.05f, 0.07f, 0.16f);

	private static readonly HashSet<ulong> ActiveCreatureNodes = [];
	private static Texture2D? _hatchTexture;

	private readonly NCreature _creature;
	private Control? _overlay;

	private HextechGlassCannonHealthBarVisual(NCreature creature)
	{
		_creature = creature;
	}

	internal static void TryAttach(NCreature? creature)
	{
		try
		{
			if (!GodotObject.IsInstanceValid(creature)
				|| !creature.IsNodeReady()
				|| creature.Entity == null)
			{
				return;
			}

			ulong creatureInstanceId = creature.GetInstanceId();
			if (!ActiveCreatureNodes.Add(creatureInstanceId))
			{
				return;
			}

			HextechGlassCannonHealthBarVisual visual = new(creature);
			TaskHelper.RunSafely(visual.RunAsync(creatureInstanceId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][GlassCannon] Could not attach health bar lock visual: {ex.Message}");
		}
	}

	private async Task RunAsync(ulong creatureInstanceId)
	{
		try
		{
			while (GodotObject.IsInstanceValid(_creature))
			{
				bool hasCap = TryGetCapRatio(_creature, out float cap);
				if (hasCap)
				{
					EnsureOverlay();
					if (GodotObject.IsInstanceValid(_overlay))
					{
						_overlay!.AnchorLeft = cap;
						_overlay.OffsetLeft = 0f;

						// 内缩,避开血条的圆角端帽与上下边,使斜线落在原版血条轮廓之内、而非外接矩形。
						float height = _overlay.Size.Y;
						if (height >= 4f)
						{
							_overlay.OffsetTop = height * 0.12f;
							_overlay.OffsetBottom = -height * 0.12f;
							_overlay.OffsetRight = -height * 0.42f;
						}

						_overlay.Visible = true;
					}
				}
				else if (GodotObject.IsInstanceValid(_overlay))
				{
					_overlay!.Visible = false;
				}

				SceneTree? tree = _creature.GetTree();
				if (tree == null)
				{
					return;
				}

				await _creature.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][GlassCannon] Health bar lock visual stopped after runtime error: {ex.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(_overlay))
			{
				_overlay!.QueueFree();
			}

			ActiveCreatureNodes.Remove(creatureInstanceId);
		}
	}

	private static bool TryGetCapRatio(NCreature creatureNode, out float cap)
	{
		cap = 0f;
		Creature? creature = creatureNode.Entity;
		if (creature == null)
		{
			return false;
		}

		// 我方:玻璃大炮遗物。
		GlassCannonRune? rune = creature.Player?.GetRelic<GlassCannonRune>();
		if (rune != null)
		{
			cap = Mathf.Clamp((float)rune.HealCapPercent, 0.05f, 0.99f);
			return true;
		}

		// 敌方:玻璃大炮敌方海克斯(整场战斗对全体敌人生效,固定封顶 70%)。
		if (creature.Monster != null
			&& creature.CombatState?.RunState is { } runState
			&& runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is { } modifier
			&& modifier.HasActiveMonsterHex(MonsterHexKind.GlassCannon))
		{
			cap = 0.7f;
			return true;
		}

		return false;
	}

	private void EnsureOverlay()
	{
		if (GodotObject.IsInstanceValid(_overlay))
		{
			return;
		}

		// NCreature →(%HealthBar)→ NCreatureStateDisplay →(%HealthBar)→ NHealthBar →(%HpForegroundContainer)→ 满血轨道。
		if (_creature.GetNodeOrNull("%HealthBar") is not { } stateDisplay
			|| stateDisplay.GetNodeOrNull<NHealthBar>("%HealthBar") is not { } bar
			|| bar.GetNodeOrNull<Control>("%HpForegroundContainer") is not { } track)
		{
			return;
		}

		Control overlay = new()
		{
			Name = OverlayName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = true,
			ZIndex = 3,
			ZAsRelative = true,
			Visible = false,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			AnchorRight = 1f,
			AnchorLeft = 0.7f,
			OffsetLeft = 0f,
			OffsetRight = 0f,
			OffsetTop = 0f,
			OffsetBottom = 0f
		};

		ColorRect dim = new()
		{
			Color = DimColor,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(dim);

		TextureRect hatch = new()
		{
			Texture = GetHatchTexture(),
			StretchMode = TextureRect.StretchModeEnum.Tile,
			TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hatch.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(hatch);

		track.AddChild(overlay);
		_overlay = overlay;
	}

	private static Texture2D GetHatchTexture()
	{
		if (_hatchTexture != null && GodotObject.IsInstanceValid(_hatchTexture))
		{
			return _hatchTexture;
		}

		// 程序化生成可无缝平铺的灰色斜线(45°)。tile=14、周期=7 → 更细更疏、边界处衔接连续。
		const int size = 14;
		const int period = 7;
		Color line = new(0.86f, 0.88f, 0.94f, 0.5f);
		Color clear = new(0f, 0f, 0f, 0f);
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				image.SetPixel(x, y, (x + y) % period < 2 ? line : clear);
			}
		}

		_hatchTexture = ImageTexture.CreateFromImage(image);
		return _hatchTexture;
	}
}
