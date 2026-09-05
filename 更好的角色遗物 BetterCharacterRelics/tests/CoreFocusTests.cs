using System.Reflection;
using System.Runtime.CompilerServices;
using BetterCharacterRelics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

internal static class CoreFocusTests
{
    private static int _focusCalls;
    private static PlayerChoiceContext? _focusContext;

    internal static void Run(Action<bool, string> check)
    {
        // 替代命令和表现边界；非首回合通过原版 AbstractModel 虚调用执行生产 postfix。
        var harmony = new Harmony("BetterCharacterRelics.Tests.Focus");
        harmony.Patch(AccessTools.Method(typeof(RelicEffects), "GainFocus"), prefix: new HarmonyMethod(typeof(CoreFocusTests), nameof(CaptureFocus)));
        harmony.Patch(AccessTools.Method(typeof(RelicEffects), "Flash"), prefix: new HarmonyMethod(typeof(CoreFocusTests), nameof(SkipFlash)));
        harmony.Patch(AccessTools.Method(typeof(RelicEffects), "ReportCoreFocus"), prefix: new HarmonyMethod(typeof(CoreFocusTests), nameof(SkipFlash)));
        try
        {
            foreach (bool infused in new[] { false, true })
            foreach (int[] rounds in new[] { new[] { 1, 2, 3, 4, 5 }, new[] { 1, 1, 1, 2, 3 } })
            {
                var fixture = new Fixture(infused);
                _focusCalls = 0;
                for (int index = 0; index < rounds.Length; index++)
                {
                    fixture.Turn(index + 1, rounds[index]);
                    Invoke(fixture.Core, fixture.Context, CombatSide.Player, [fixture.Creature], fixture.Combat);
                    check(_focusCalls == (infused || index >= 2 ? 1 : 0), $"Focus timing: infused={infused}; personal turn={index + 1}; round={rounds[index]}");
                    if (!infused && index >= 2) check(ReferenceEquals(_focusContext, fixture.Context), "Cracked Core discarded the hook choice context");
                }

                fixture.Turn(infused ? 1 : 3, 3);
                int previous = _focusCalls;
                Invoke(fixture.Core, fixture.Context, CombatSide.Enemy, [], fixture.Combat);
                Invoke(fixture.Core, fixture.Context, CombatSide.Player, [], fixture.Combat);
                check(_focusCalls == previous, "Core triggered for an enemy or a teammate's extra turn");
            }

            foreach (bool infused in new[] { false, true })
            {
                var fixture = new Fixture(infused);
                fixture.Turn(infused ? 1 : 3, 1);
                _focusCalls = 0;
                var original = new TaskCompletionSource();
                Task result = infused
                    ? RelicEffects.InfusedCoreAfterSideTurnStartAfterOriginal(original.Task, (InfusedCore)fixture.Core, CombatSide.Player, [fixture.Creature], fixture.Combat)
                    : RelicEffects.CrackedCoreBeforeSideTurnStartAfterOriginal(original.Task, (CrackedCore)fixture.Core, fixture.Context, CombatSide.Player, [fixture.Creature], fixture.Combat);
                check(!result.IsCompleted && _focusCalls == 0, "Focus did not wait for the original relic task");
                original.SetResult();
                result.GetAwaiter().GetResult();
                check(_focusCalls == 1, "Focus missing after original relic task completed");
            }
            Console.WriteLine("Core focus: normal/extra turns, participant isolation, task ordering and choice context passed.");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private sealed class Fixture
    {
        internal readonly Player Player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        internal readonly Creature Creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
        internal readonly PlayerCombatState State = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        internal readonly ICombatState Combat = DispatchProxy.Create<ICombatState, CombatProxy>();
        internal readonly PlayerChoiceContext Context = new ThrowingPlayerChoiceContext();
        internal readonly RelicModel Core;

        internal Fixture(bool infused)
        {
            Set(Player, "Creature", Creature);
            Set(Player, "PlayerCombatState", State);
            Set(Creature, "Player", Player);
            Set(Creature, "Side", CombatSide.Player);
            Core = infused ? new InfusedCore() : new CrackedCore();
            Set(Core, "IsMutable", true, typeof(AbstractModel));
            Core.Owner = Player;
        }

        internal void Turn(int turn, int round)
        {
            Set(State, "TurnNumber", turn);
            ((CombatProxy)(object)Combat).Round = round;
        }
    }

    private static void Set(object instance, string property, object value, Type? declaring = null)
        => AccessTools.Field(declaring ?? instance.GetType(), "<" + property + ">k__BackingField").SetValue(instance, value);

    private static bool CaptureFocus(PlayerChoiceContext choiceContext, decimal amount, ref Task __result)
    {
        if (amount != 1m) throw new Exception("Core focus amount changed");
        _focusContext = choiceContext;
        _focusCalls++;
        __result = Task.CompletedTask;
        return false;
    }

    private static bool SkipFlash() => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Invoke(AbstractModel core, PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (((RelicModel)core).Owner.PlayerCombatState!.TurnNumber == 1)
        {
            // 首回合原版充能依赖 Godot 原生层；从生产 postfix 入口检查追加效果。
            Task result = Task.CompletedTask;
            if (core is CrackedCore cracked) CrackedCoreBeforeSideTurnStartPatch.Postfix(cracked, context, side, participants, combat, ref result);
            else InfusedCoreAfterSideTurnStartPatch.Postfix((InfusedCore)core, side, participants, combat, ref result);
            result.GetAwaiter().GetResult();
            return;
        }
        if (core is CrackedCore) core.BeforeSideTurnStart(context, side, participants, combat).GetAwaiter().GetResult();
        else core.AfterSideTurnStart(side, participants, combat).GetAwaiter().GetResult();
    }

    public class CombatProxy : DispatchProxy
    {
        public int Round;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_RoundNumber" ? Round : throw new NotSupportedException(method?.Name);
    }
}
