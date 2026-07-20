namespace HextechRunes;

public sealed partial class SolidTimeRune
{
	private static readonly MethodInfo CardOnPlayMethod = typeof(CardModel).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("CardModel.OnPlay was not found.");

	private Creature? PickTarget(CardModel card, HextechCombatState combatState, int index)
	{
		return card.TargetType switch
		{
			TargetType.AnyEnemy => HextechRuneTargeting.PickRandomHittableEnemy(
				Owner,
				combatState,
				"solid-time",
				combatState.RoundNumber.ToString(),
				index.ToString(),
				card.Id.Entry),
			TargetType.AnyAlly => Owner?.Creature,
			TargetType.AnyPlayer => Owner?.Creature,
			_ => null
		};
	}

	private static async Task ApplyStoredPowerDirectly(PlayerChoiceContext choiceContext, CardModel card, Creature? target)
	{
		bool addedToTemporaryPlayPile = false;
		if (card.Pile == null)
		{
			await CardPileCmd.Add(card, PileType.Play, skipVisuals: true);
			addedToTemporaryPlayPile = card.Pile?.Type == PileType.Play;
			if (!addedToTemporaryPlayPile)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] SolidTime skipped stored power without combat pile: card={card.Id}");
				return;
			}
		}

		CardPlay cardPlay = new()
		{
			Card = card,
#if STS2_109_OR_NEWER
			// 0.109.0 起 CardPlay 新增 required Player(打出者);回放存储 power 的打出者即卡牌所有者。
			Player = card.Owner,
#endif
			Target = target,
			ResultPile = PileType.None,
			Resources = new ResourceInfo
			{
				EnergySpent = 0,
				EnergyValue = 0,
				StarsSpent = 0,
				StarValue = 0
			},
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};

		choiceContext.PushModel(card);
		try
		{
			if (!await TryApplySolidTimeSpecialCase(card))
			{
				await (Task)GetOnPlayMethod(card).Invoke(card, [choiceContext, cardPlay])!;
			}

			if (!card.Owner.Creature.IsDead)
			{
				card.InvokeExecutionFinished();
			}
		}
		finally
		{
			choiceContext.PopModel(card);
			if (addedToTemporaryPlayPile && card.Pile?.IsCombatPile == true)
			{
				await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
			}
		}
	}

	private static async Task<bool> TryApplySolidTimeSpecialCase(CardModel card)
	{
		if (card is VoidForm)
		{
			await PowerCmd.Apply<VoidFormPower>(
				card.Owner.Creature,
				card.DynamicVars["VoidFormPower"].BaseValue,
				card.Owner.Creature,
				card);
			return true;
		}

		return false;
	}

	private static MethodInfo GetOnPlayMethod(CardModel card)
	{
		Type? type = card.GetType();
		while (type != null)
		{
			MethodInfo? method = type.GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			if (method != null)
			{
				return method;
			}

			type = type.BaseType;
		}

		return CardOnPlayMethod;
	}
}
