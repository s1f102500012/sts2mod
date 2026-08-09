using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;

namespace IntegratedStrategyEvents.Events;

public sealed partial class HeavyContractEvent : IntegratedStrategyEventModel
{
	private const int HelpCardCount = 1;
	private const int OverturnHpLoss = 12;
	private const int OverturnCardCount = 2;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		Player owner = OwnerOrThrow;
		return
		[
			CreateHelpOption(),
			CreateOverturnOption(owner),
			Choice(Leave, "LEAVE")
		];
	}

	private EventOption CreateHelpOption()
	{
		return HasRemovableDeckCards(HelpCardCount)
			? Choice(Help, "HELP")
			: LockedChoice("HELP_LOCKED");
	}

	private EventOption CreateOverturnOption(Player owner)
	{
		if (!HasRemovableDeckCards(OverturnCardCount))
		{
			return LockedChoice("OVERTURN_LOCKED_CARDS");
		}

		return HpChoice(
			owner,
			OverturnHpLoss,
			Overturn,
			"OVERTURN",
			"OVERTURN_LOCKED_HP");
	}

	private async Task Help()
	{
		await RemoveDeckCards(HelpCardCount);
		Finish("HELP");
	}

	private async Task Overturn()
	{
		await LoseHp(OverturnHpLoss);
		await RemoveDeckCards(OverturnCardCount);
		Finish("OVERTURN");
	}

	private Task Leave()
	{
		Finish("LEAVE");
		return Task.CompletedTask;
	}
}
