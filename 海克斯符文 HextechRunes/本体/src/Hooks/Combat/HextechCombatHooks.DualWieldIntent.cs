using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	// 敌方「双刀流」意图预览:让头顶攻击意图和实战保持一致——每段白值减半(向上取整)、段数加倍。
	//
	// 实战由 DualWieldAttackCommandExecutePrefix 改写 AttackCommand 的 _damagePerHit/_hitCount 实现;
	// 但意图是攻击执行前单独算好显示的,不经过 AttackCommand.Execute,所以光改实战意图不会变。
	//
	// 意图有两条对玩家可见的渲染路径,都从 MonsterModel.NextMove.Intents 拿原始意图:
	//   1. 头顶意图数字/贴图    —— NCreature.UpdateIntent → NIntent.UpdateIntent(intent, targets, owner)
	//   2. 悬停 tooltip(hover) —— NIntent.OnHovered 与 Creature.HoverTips 都最终调 AbstractIntent.GetHoverTip
	// 两处都给我们 owner,因此在这两个入口各加一道 prefix:当 owner 是敌人且双刀流生效时,把原始
	// AttackIntent 换成等价的「双段」意图(DamageCalc 包一层白值减半,Repeats×2)再让原逻辑继续,于是
	// 数字、贴图、单攻显示成「每段伤害×段数」、以及 hover 描述里的伤害/段数全部一致。
	//
	// 纯无状态转换:每次都基于原始意图的 DamageCalc 重新计算,既不改原意图对象、也不读回自己上次产出的
	// 意图,因此意图反复刷新也不会把伤害越减越少。
	private static void InstallDualWieldIntentHooks(Harmony harmony)
	{
		try
		{
			harmony.Patch(
				RequireMethod(
					typeof(NIntent),
					nameof(NIntent.UpdateIntent),
					BindingFlags.Instance | BindingFlags.Public,
					typeof(AbstractIntent),
					typeof(IEnumerable<Creature>),
					typeof(Creature)),
				prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(NIntentUpdateIntentPrefix)));

			harmony.Patch(
				RequireMethod(
					typeof(AbstractIntent),
					nameof(AbstractIntent.GetHoverTip),
					BindingFlags.Instance | BindingFlags.Public,
					typeof(IEnumerable<Creature>),
					typeof(Creature)),
				prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(AbstractIntentGetHoverTipPrefix)));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] 双刀流意图预览 hook 安装失败,意图显示可能与实际伤害不一致: {ex.GetType().Name}: {ex.Message}");
		}
	}

	// 头顶意图数字/贴图:把传入的攻击意图替换成「双段」等价意图。
	private static void NIntentUpdateIntentPrefix(ref AbstractIntent intent, Creature owner)
	{
		try
		{
			if (TryCreateDualWieldIntent(intent, owner, out DualWieldAttackIntent? transformed) && transformed != null)
			{
				intent = transformed;
			}
		}
		catch (Exception ex)
		{
			LogDualWieldIntentFailure(nameof(NIntentUpdateIntentPrefix), ex);
		}
	}

	// 意图 hover tooltip:NIntent.OnHovered 与 Creature.HoverTips 都汇到这里。用「双段」等价意图生成
	// hover tip,让悬停描述里的每段伤害/段数也和实战一致。
	private static bool AbstractIntentGetHoverTipPrefix(
		AbstractIntent __instance,
		IEnumerable<Creature> targets,
		Creature owner,
		ref HoverTip __result)
	{
		try
		{
			if (TryCreateDualWieldIntent(__instance, owner, out DualWieldAttackIntent? transformed) && transformed != null)
			{
				// transformed 自身是 DualWieldAttackIntent,再次进入本 prefix 会被下方类型守卫挡掉,不会递归。
				__result = transformed.GetHoverTip(targets, owner);
				return false;
			}
		}
		catch (Exception ex)
		{
			// prefix 当场跑 DamageCalc→ModifyDamage 链,异常会打断调用方;失败放行原版。
			LogDualWieldIntentFailure(nameof(AbstractIntentGetHoverTipPrefix), ex);
		}

		return true;
	}

	private static void LogDualWieldIntentFailure(string hook, Exception ex)
	{
		if (HextechRunLogBudget.TryConsume("combat.dual-wield-intent-failure", 10))
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] {hook} failed; falling back to vanilla intent: {ex}");
		}
	}

	// 仅当 owner 是敌人、双刀流生效、且 intent 是尚未转换过的攻击意图时,产出等价的「双段」意图。
	private static bool TryCreateDualWieldIntent(
		AbstractIntent intent,
		Creature owner,
		out DualWieldAttackIntent? transformed)
	{
		transformed = null;

		// 已经是我们替换出来的双段意图就别再套娃(防重复减半/加段、也防 GetHoverTip 递归)。
		if (intent is not AttackIntent attackIntent || attackIntent is DualWieldAttackIntent)
		{
			return false;
		}

		if (owner?.Side != CombatSide.Enemy
			|| owner.CombatState?.RunState is not RunState runState
			|| HextechMayhemModifier.FindIn(runState) is not { } modifier
			|| !modifier.HasActiveMonsterHex(MonsterHexKind.DualWield))
		{
			return false;
		}

		Func<decimal>? originalDamageCalc = attackIntent.DamageCalc;
		if (originalDamageCalc == null)
		{
			return false;
		}

		int doubledRepeats = Math.Max(1, attackIntent.Repeats) * 2;
		transformed = new DualWieldAttackIntent(
			() =>
			{
				decimal white = originalDamageCalc();
				// 与 DualWieldAttackCommandExecutePrefix 完全一致:白值 >= 1 才减半(向上取整);
				// 计算型/非正值伤害保持原样,只翻倍段数。减半发生在力量等加成之前(改白值不改系数),
				// GetSingleDamage 随后照常走 Hook.ModifyDamage 叠加加成,于是和实战逐段伤害对得上。
				return white >= 1m ? Math.Ceiling(white / 2m) : white;
			},
			doubledRepeats);
		return true;
	}
}

/// <summary>
/// 双刀流意图预览专用的「多段攻击意图」:行为等同原版 <c>MultiAttackIntent</c>,但允许直接注入一个
/// 已经做过「白值减半」处理的 <see cref="Func{Decimal}"/> 伤害计算器(原版 MultiAttackIntent 的构造器
/// 只收 int,无法承载动态/减半后的白值)。其余 label/贴图/动画/hover 描述全部沿用 AttackIntent 基类逻辑。
/// </summary>
internal sealed class DualWieldAttackIntent : AttackIntent
{
	private readonly int _repeats;

	public DualWieldAttackIntent(Func<decimal> damageCalc, int repeats)
	{
		DamageCalc = damageCalc;
		_repeats = repeats;
	}

	public override int Repeats => _repeats;

	protected override LocString IntentLabelFormat => new LocString("intents", "FORMAT_DAMAGE_MULTI");

	public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
	{
		return GetSingleDamage(targets, owner) * Repeats;
	}

	public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
	{
		LocString format = IntentLabelFormat;
		format.Add("Damage", GetSingleDamage(targets, owner));
		format.Add("Repeat", Repeats);
		return format;
	}
}
