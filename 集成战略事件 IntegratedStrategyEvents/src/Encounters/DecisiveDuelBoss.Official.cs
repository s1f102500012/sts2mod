#if STS2_109_OR_NEWER
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
namespace IntegratedStrategyEvents.Encounters;
public abstract partial class DecisiveDuelBoss
{
	private Task DamageOtherUnits(IEnumerable<Creature> targets, decimal damage) =>
		CreatureCmd.Damage(new BlockingPlayerChoiceContext(), targets, damage, ValueProp.Move, Creature, null, null);
}
#endif
