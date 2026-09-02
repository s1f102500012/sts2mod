using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

/// <summary>
/// 给「灼烧」(<see cref="HextechBurnPower"/>) 加上和原版「中毒」一样的血条预测渲染：
/// 在生命值末端画出下次结算会损失的血量。橙色的烧条与绿色的毒条按各自实际结算先后排列
/// （先结算的贴最右、后结算的在其左侧），并把灼烧伤害纳入「灾厄」致死判定。
///
/// 原版 <c>NHealthBar.RefreshForeground</c> 硬编码只认 Poison/Doom，且场景里只有
/// <c>_poisonForeground</c>/<c>_doomForeground</c> 两个覆盖节点，模组无法改场景新增节点，
/// 因此在运行时克隆一个橙色 burn 前景节点，并整段前缀替换该方法以容纳三段几何。
/// </summary>
internal static class HextechBurnHealthBarHooks
{
	private static readonly Color BurnForegroundColor = new(1f, 0.65f, 0.08f);

	// 灼烧斩杀时血条数字的字体色/描边色（与毒的绿 #76FF40、灾厄的紫 #FB8DFF 并列的琥珀黄）。
	private static readonly Color BurnLethalFontColor = new("FFC233");
	private static readonly Color BurnLethalOutlineColor = new("3A1E00");
	private static readonly Color DoomLethalFontColor = new("FB8DFF");
	private static readonly Color DoomLethalOutlineColor = new("2D1263");

	private static readonly StringName FontColorOverride = "font_color";
	private static readonly StringName FontOutlineColorOverride = "font_outline_color";

	private static readonly ConditionalWeakTable<NHealthBar, Control> BurnForegrounds = new();

	private static FieldInfo _creatureField = null!;
	private static FieldInfo _hpForegroundField = null!;
	private static FieldInfo _poisonForegroundField = null!;
	private static FieldInfo _doomForegroundField = null!;
	private static FieldInfo _hpLabelField = null!;
	private static PropertyInfo _maxFgWidthProp = null!;
	private static MethodInfo _getFgWidthMethod = null!;

	/// <summary>解析血条私有成员;任一缺失即抛出,两个补丁类随之停用。</summary>
	private static void EnsureMembers()
	{
		if (_getFgWidthMethod != null)
		{
			return;
		}

		_creatureField = RequireField("_creature");
		_hpForegroundField = RequireField("_hpForeground");
		_poisonForegroundField = RequireField("_poisonForeground");
		_doomForegroundField = RequireField("_doomForeground");
		_hpLabelField = RequireField("_hpLabel");
		_maxFgWidthProp = typeof(NHealthBar).GetProperty("MaxFgWidth", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new MissingMemberException(nameof(NHealthBar), "MaxFgWidth");
		_getFgWidthMethod = typeof(NHealthBar).GetMethod("GetFgWidth", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(int) }, null)
			?? throw new MissingMethodException(nameof(NHealthBar), "GetFgWidth");
	}

	private static FieldInfo RequireField(string name)
	{
		return typeof(NHealthBar).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(nameof(NHealthBar), name);
	}

	/// <summary>
	/// 整段替换 <c>RefreshForeground</c>。任意环节异常时返回 true 让原版方法照常执行，
	/// 保证最坏情况只是不显示烧条、绝不破坏血条。
	/// </summary>

	/// <summary>
	/// 在原版给血条数字上色之后，仅当「灼烧」参与斩杀时覆盖字色，做出和毒(绿)/灾厄(紫)并列的斩杀提示。
	/// 灼烧不影响结果时完全不动 vanilla 着色（含格挡色、无敌色）。
	/// </summary>

	private static bool TryRenderForeground(NHealthBar instance)
	{
		if (_creatureField.GetValue(instance) is not Creature creature)
		{
			return false;
		}

		int currentHp = creature.CurrentHp;
		int burn = PredictBurnDamage(creature, currentHp);
		if (burn <= 0)
		{
			if (BurnForegrounds.TryGetValue(instance, out Control? existingBurnForeground)
				&& GodotObject.IsInstanceValid(existingBurnForeground))
			{
				existingBurnForeground.Visible = false;
			}

			return false;
		}

		Control hpForeground = (Control)_hpForegroundField.GetValue(instance)!;
		Control poisonForeground = (Control)_poisonForegroundField.GetValue(instance)!;
		Control doomForeground = (Control)_doomForegroundField.GetValue(instance)!;
		Control burnForeground = GetOrCreateBurnForeground(instance, poisonForeground);

		if (currentHp <= 0)
		{
			poisonForeground.Visible = false;
			doomForeground.Visible = false;
			burnForeground.Visible = false;
			hpForeground.Visible = false;
			return true;
		}

		// 无限生命（无敌）等特殊显示交回原版，避免缺少其专用配色常量；先藏掉烧条防残留。
		if (creature.HpDisplay.IsInfinite())
		{
			burnForeground.Visible = false;
			return false;
		}

		float maxFgWidth = (float)_maxFgWidthProp.GetValue(instance)!;
		hpForeground.Visible = true;
		float offsetRight = FgWidth(instance, currentHp) - maxFgWidth;
		hpForeground.OffsetRight = offsetRight;

		int poison = creature.GetPower<PoisonPower>()?.CalculateTotalDamageNextTurn() ?? 0;
		bool hasPoison = poison > 0 && creature.HasPower<PoisonPower>();
		bool hasBurn = burn > 0;

		// 按实际结算先后排列：先结算的段贴最右（最先从当前生命值扣起）。
		var segments = new List<(int Amount, Control Node)>();
		if (hasPoison && hasBurn)
		{
			if (PoisonResolvesBeforeBurn(creature))
			{
				segments.Add((poison, poisonForeground));
				segments.Add((burn, burnForeground));
			}
			else
			{
				segments.Add((burn, burnForeground));
				segments.Add((poison, poisonForeground));
			}
		}
		else if (hasPoison)
		{
			segments.Add((poison, poisonForeground));
		}
		else if (hasBurn)
		{
			segments.Add((burn, burnForeground));
		}

		if (!hasPoison)
		{
			poisonForeground.Visible = false;
			poisonForeground.OffsetLeft = 0f;
		}

		if (!hasBurn)
		{
			burnForeground.Visible = false;
			burnForeground.OffsetLeft = 0f;
		}

		int remainingHp = currentHp;
		foreach ((int amount, Control node) in segments)
		{
			if (remainingHp <= 0)
			{
				node.Visible = false;
				continue;
			}

			int afterHp = Math.Max(0, remainingHp - amount);
			node.Visible = true;
			if (afterHp <= 0)
			{
				// 该段吃光剩余生命（斩杀）：和原版毒/灾厄致死一样整段覆盖到血条最左，
				// 不走 nine-patch 边距，避免左侧露出一截没盖住的小条。
				node.OffsetLeft = 0f;
			}
			else
			{
				int patchMarginLeft = node is NinePatchRect ninePatch ? ninePatch.PatchMarginLeft : 0;
				node.OffsetLeft = Math.Max(0f, FgWidth(instance, afterHp) - patchMarginLeft);
			}

			node.OffsetRight = FgWidth(instance, remainingHp) - maxFgWidth;
			remainingHp = afterHp;
		}

		int totalDotDamage = poison + burn;
		hpForeground.OffsetRight = FgWidth(instance, remainingHp) - maxFgWidth;
		hpForeground.Visible = remainingHp > 0;

		RenderDoom(instance, creature, hpForeground, doomForeground, maxFgWidth, currentHp, totalDotDamage);
		return true;
	}

	private static void RenderDoom(
		NHealthBar instance,
		Creature creature,
		Control hpForeground,
		Control doomForeground,
		float maxFgWidth,
		int currentHp,
		int totalDotDamage)
	{
		int doom = creature.GetPowerAmount<DoomPower>();
		if (!creature.HasPower<DoomPower>() || doom <= 0)
		{
			doomForeground.Visible = false;
			return;
		}

		doomForeground.Visible = true;
		float doomWidth = FgWidth(instance, doom) - maxFgWidth;
		bool doomLethal = doom >= currentHp - totalDotDamage; // 灾厄按「持续伤害结算后」是否仍致死
		bool dotLethal = totalDotDamage >= currentHp;
		if (doomLethal)
		{
			if (!dotLethal)
			{
				doomForeground.OffsetRight = hpForeground.OffsetRight;
				hpForeground.Visible = false;
			}
			else
			{
				hpForeground.Visible = false;
				doomForeground.Visible = false;
			}
		}
		else
		{
			int patchMarginRight = doomForeground is NinePatchRect ninePatch ? ninePatch.PatchMarginRight : 0;
			doomForeground.OffsetRight = Math.Min(0f, doomWidth + patchMarginRight);
			hpForeground.Visible = true;
		}
	}

	/// <summary>下次灼烧结算的预测掉血，与 <see cref="HextechBurnPower"/> 的结算公式一致（与中毒一样忽略格挡）。</summary>
	private static int PredictBurnDamage(Creature creature, int currentHp)
	{
		int stacks = creature.GetPowerAmount<HextechBurnPower>();
		if (stacks <= 0)
		{
			return 0;
		}

		int percentHpLoss = (int)Math.Floor((decimal)currentHp * stacks / 100m);
		return Math.Max(stacks, percentHpLoss);
	}

	/// <summary>
	/// 玩家身上：中毒在回合开始结算、灼烧在回合结束结算 → 中毒先。
	/// 敌人身上：两者都在回合开始结算 → 按 power 施加先后（<see cref="Creature.Powers"/> 列表顺序）。
	/// </summary>
	private static bool PoisonResolvesBeforeBurn(Creature creature)
	{
		if (creature.Side == CombatSide.Player)
		{
			return true;
		}

		return IndexOfPower<PoisonPower>(creature) <= IndexOfPower<HextechBurnPower>(creature);
	}

	private static int IndexOfPower<TPower>(Creature creature)
		where TPower : PowerModel
	{
		IReadOnlyList<PowerModel> powers = creature.Powers;
		for (int i = 0; i < powers.Count; i++)
		{
			if (powers[i] is TPower)
			{
				return i;
			}
		}

		return int.MaxValue;
	}

	private static Control GetOrCreateBurnForeground(NHealthBar instance, Control poisonForeground)
	{
		if (BurnForegrounds.TryGetValue(instance, out Control? existing) && GodotObject.IsInstanceValid(existing))
		{
			return existing;
		}

		var clone = (Control)poisonForeground.Duplicate();
		clone.Name = "HextechBurnForeground";
		clone.SelfModulate = BurnForegroundColor;
		clone.Visible = false;
		poisonForeground.GetParent().AddChild(clone);
		BurnForegrounds.AddOrUpdate(instance, clone);
		return clone;
	}

	private static float FgWidth(NHealthBar instance, int amount)
	{
		return (float)_getFgWidthMethod.Invoke(instance, new object[] { amount })!;
	}

	[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
	[HextechPatch("ui.burn-health-bar.foreground", "灼烧血条预测")]
	private static class RefreshForegroundPatch
	{
		[HarmonyPrepare]
		private static bool Prepare()
		{
			EnsureMembers();
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(NHealthBar __instance)
		{
			try
			{
				return !TryRenderForeground(__instance);
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Burn health bar render failed; falling back to vanilla: {ex.GetType().Name}: {ex.Message}");
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(NHealthBar), "RefreshText")]
	[HextechPatch("ui.burn-health-bar.text", "灼烧血条预测")]
	private static class RefreshTextPatch
	{
		[HarmonyPrepare]
		private static bool Prepare()
		{
			EnsureMembers();
			return true;
		}

		[HarmonyPostfix]
		private static void Postfix(NHealthBar __instance)
		{
			try
			{
				if (_creatureField.GetValue(__instance) is not Creature creature)
				{
					return;
				}

				int currentHp = creature.CurrentHp;
				if (currentHp <= 0 || !creature.HpDisplay.ShowsNumbers() || creature.HpDisplay.IsInfinite())
				{
					return;
				}

				int burn = PredictBurnDamage(creature, currentHp);
				if (burn <= 0)
				{
					// 没有灼烧：毒/灾厄/默认的着色完全交给 vanilla。
					return;
				}

				int poison = creature.GetPower<PoisonPower>()?.CalculateTotalDamageNextTurn() ?? 0;
				if (poison >= currentHp)
				{
					// 毒单独已致死：保留 vanilla 的绿色斩杀。
					return;
				}

				int totalDot = poison + burn;
				Color fontColor;
				Color outlineColor;
				if (totalDot >= currentHp)
				{
					// 毒+烧合计致死：灼烧斩杀色（琥珀黄）。
					fontColor = BurnLethalFontColor;
					outlineColor = BurnLethalOutlineColor;
				}
				else
				{
					int doom = creature.GetPowerAmount<DoomPower>();
					bool doomLethalWithBurn = creature.HasPower<DoomPower>() && doom > 0 && doom >= currentHp - totalDot;
					bool doomLethalVanilla = doom >= currentHp - poison;
					if (!doomLethalWithBurn || doomLethalVanilla)
					{
						// 灼烧没有改变斩杀判定：维持 vanilla 着色。
						return;
					}

					// 灼烧把「灾厄」推成致死：补上灾厄斩杀色（紫）。
					fontColor = DoomLethalFontColor;
					outlineColor = DoomLethalOutlineColor;
				}

				Control hpLabel = (Control)_hpLabelField.GetValue(__instance)!;
				hpLabel.AddThemeColorOverride(FontColorOverride, fontColor);
				hpLabel.AddThemeColorOverride(FontOutlineColorOverride, outlineColor);
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Burn health bar text recolor failed; leaving vanilla color: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}
