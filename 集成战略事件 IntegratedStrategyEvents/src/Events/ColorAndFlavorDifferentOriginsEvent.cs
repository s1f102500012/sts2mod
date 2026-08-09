using MegaCrit.Sts2.Core.Events;

namespace IntegratedStrategyEvents.Events;

public sealed partial class ColorAndFlavorDifferentOriginsEvent : IntegratedStrategyEventModel
{
	private const int MaxHpGain = 6;
	private const int HealAmount = 12;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		return
		[
			Choice(TakeApple, "TAKE_APPLE"),
			Choice(TakeFish, "TAKE_FISH"),
			Choice(Leave, "LEAVE")
		];
	}

	private async Task TakeApple()
	{
		await GainMaxHp(MaxHpGain);
		Finish("TAKE_APPLE");
	}

	private async Task TakeFish()
	{
		await Heal(HealAmount);
		Finish("TAKE_FISH");
	}

	private Task Leave()
	{
		Finish("LEAVE");
		return Task.CompletedTask;
	}
}
