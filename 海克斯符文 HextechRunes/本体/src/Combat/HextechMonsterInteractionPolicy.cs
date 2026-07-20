using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HextechRunes;

internal static class HextechMonsterInteractionPolicy
{
	public static bool IsTrueCombatDeath(Creature creature)
	{
		return IsTrueCombatDeath(creature, out _);
	}

	public static bool IsTrueCombatDeath(Creature creature, [NotNullWhen(true)] out HextechCombatState? combatState)
	{
		combatState = creature.CombatState;
		return combatState != null
			&& !IsBossPhaseTransitionDeath(creature)
			&& Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature);
	}

	public static bool IsBossPhaseTransitionDeath(Creature creature)
	{
		HextechCombatState? combatState = creature.CombatState;
		return combatState != null
			&& creature.Monster is TestSubject
			&& !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature);
	}

	public static bool ShouldIgnoreMonsterSelfBuff(PowerModel power)
	{
		return IsStructuralMonsterBuff(power);
	}

	// 结构性怪物 buff = 剥除会让遭遇脚本/回合流转/位置关系断裂的机制 power。
	// 曾把全部"动画/形态状态机"类也列入(防幽灵鳗 SkittishPower 卡死),现已逐个核验收窄:
	//  - Skittish 的卡死根因是 Block 出场动画悬空,已由 RemoveMonsterBuffSafely 补出场后安全剥除;
	//  - Smoggy/Burrowed/Hibernate/CurlUp/HardenedShell/SentryMode/Shadowmeld/Shroud/Sneaky/
	//    Covered/Surprise/Soar/Flutter/SpectrumShift 反编译核验为一次性动画或自带 AfterRemoved 清理,可正常剥除;
	//  - Asleep/Slumber 与怪物唤醒脚本硬耦合((LagavulinMatriarch/SlumberingBeetle)强转+IsAwake/WakeUpMove),
	//    直接剥除会绕过唤醒流程破坏行动 AI,保留跳过。
	// 数值成长类(力量/CreativeAi 等)不在此列,允许正常剥除。
	public static bool IsStructuralMonsterBuff(PowerModel power)
	{
		return power is SandpitPower
			or ReattachPower
			or AdaptablePower
			// 怪物唤醒脚本耦合
			or AsleepPower
			or SlumberPower
#if STS2_108_OR_NEWER
			// 0.108 新增类型
			or SoulboundPower
#endif
			// 遭遇脚本/演出/时限
			or BattlewornDummyTimeLimitPower
			or MonologuePower
			or HatchPower
			or CountdownPower
			or TheSealedThronePower
			or WitheringPresencePower
			or PillarOfCreationPower
			or ChildOfTheStarsPower
			or PaleBlueDotPower
			or TheHuntPower
			or DemesnePower
			or NemesisPower
			or HardToKillPower
			// 位置/关系
			or BackAttackLeftPower
			or BackAttackRightPower
			or UnmovablePower
			or OrbitPower
			or MinionPower
			or GuardedPower
			or InterceptPower
			or DieForYouPower
			or IllusionPower
			or InfestedPower
			or FastenPower
			or HauntPower
			// 意图预告
			or SummonNextTurnPower
			or StarNextTurnPower
			or SteamEruptionPower;
	}

	/// <summary>
	/// 怪物专属机制/形态类 buff:不适合镜像/转移到玩家身上
	/// (动画触发器、怪物行动脚本或受击目标语义在玩家模型上不存在)。
	/// 范围 = 结构性 buff + 已放开剥除的动画/形态 buff + 已确认与怪物侧语义耦合的机制 buff
	/// (可剥不代表可镜像)。
	/// </summary>
	public static bool IsMonsterMechanismBuff(PowerModel power)
	{
		return IsStructuralMonsterBuff(power)
			|| power is SkittishPower
				or SmoggyPower
				or BurrowedPower
#if STS2_108_OR_NEWER
				or HibernatePower
#endif
				or CurlUpPower
				or HardenedShellPower
				or SentryModePower
				or ShadowmeldPower
				or ShroudPower
				or SneakyPower
				or CoveredPower
				or SurprisePower
				or SoarPower
				or FlutterPower
				or SpectrumShiftPower
				// 养蜂人「个人蜂巢」默认攻击者是玩家,并把晕眩塞进 dealer.Player 的牌堆。
				// 薄暮法衣镜像到玩家后攻击者是怪物,dealer.Player 为空并会中断受击任务链。
				or PersonalHivePower
				// 机器人「库存」(Stock):囤积-释放机制与敌人行动脚本耦合,薄暮法衣镜像到玩家会卡死(玩家实报)。
				or StockPower;
	}

	/// <summary>
	/// 安全剥除怪物增益:对有"进场动画等待出场"状态机的 power(幽灵鳗 Skittish),先补出场动画再移除,
	/// 否则 Spine 状态机停在 Block 态、下一次动画触发永远等不到(玩家实报"感受燃烧打四鳗卡死"的根因)。
	/// 感受燃烧/升级:暴露的剥除一律走这里。
	/// </summary>
	public static async Task RemoveMonsterBuffSafely(PowerModel power)
	{
		if (power is SkittishPower { HasGainedBlockThisTurn: true } && power.Owner != null)
		{
			SfxCmd.Play("event:/sfx/enemy/enemy_attacks/phantasmal_gardeners/phantasmal_gardeners_extend");
			await CreatureCmd.TriggerAnim(power.Owner, "BlockEnd", 0.15f);
		}

		await PowerCmd.Remove(power);
	}
}
