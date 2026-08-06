using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal sealed class HextechForgeChoiceReward : Reward
{
	private readonly List<RelicModel> _options;

	public HextechForgeChoiceReward(IReadOnlyList<RelicModel> options, Player player)
		: base(player)
	{
		_options = options.Select(CreateMutableOption).ToList();
	}

	protected override RewardType RewardType => RewardType.Relic;

	public override int RewardsSetIndex => 4;

	public override LocString Description => new("relic_collection", "HEXTECH_FORGE_CHOICE_REWARD");

	public override bool IsPopulated => _options.Count > 0;

	protected override string IconPath => GetForgeRewardIconPath();

	internal ModelId ClaimedForgeId { get; private set; } = ModelId.none;

#if STS2_105_OR_NEWER
	public override void Populate()
	{
		MarkContentAsSeen();
	}
#else
	public override Task Populate()
	{
		MarkContentAsSeen();
		return Task.CompletedTask;
	}
#endif

	protected override async Task<bool> OnSelect()
	{
		// RewardSynchronizer already broadcasts the obtained forge. Reserving a
		// PlayerChoiceSynchronizer id here is unsafe because reward OnSelect runs
		// only on the choosing client, which desyncs vanilla choice counters.
		RelicModel? selected = await HextechForgeSelectionCoordinator.SelectForge(Player, _options, "reward", syncMultiplayerChoice: false);
		if (selected == null)
		{
			return false;
		}

		ClaimedForgeId = selected.CanonicalInstance?.Id ?? selected.Id;
		await HextechForgeGrantHelper.ObtainSelectedForge(Player, selected, syncObtainedRelic: true);
		HextechLog.Info($"[{ModInfo.Id}][ForgeChoiceReward] Obtained selected forge: player={Player.NetId} relic={(selected.CanonicalInstance?.Id ?? selected.Id).Entry}");
		return true;
	}

	public override SerializableReward ToSerializable()
	{
		return new SerializableReward
		{
			// Gold rewards deserialize safely before our postfix replaces the marker with this custom reward.
			RewardType = RewardType.Gold,
			GoldAmount = 0,
			CardPoolIds = _options.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id).ToList(),
			OptionCount = _options.Count,
			CustomDescriptionEncounterSourceId = ModelDb.GetId<RandomForgeShopRelic>(),
		};
	}

	public override void MarkContentAsSeen()
	{
		foreach (RelicModel relic in _options)
		{
			SaveManager.Instance.MarkRelicAsSeen(relic);
		}
	}

	internal static bool TryFromSavedReward(
		SerializableReward save,
		Player player,
		out HextechForgeChoiceReward? reward)
	{
		reward = null;
		int requestedCount = Math.Clamp(save.OptionCount, 0, HextechStableModelIdListCodec.MaxCount);
		if (requestedCount == 0)
		{
			LogForgeRestoreSkip(ModelId.none, $"invalid option count {save.OptionCount}");
			return false;
		}

		List<RelicModel> options = new(requestedCount);
		int scanned = 0;
		foreach (ModelId id in save.CardPoolIds)
		{
			if (scanned++ >= HextechStableModelIdListCodec.MaxCount)
			{
				LogForgeRestoreSkip(id, "saved option list exceeds the supported limit");
				break;
			}

			try
			{
				AbstractModel? model = ModelDb.GetByIdOrNull<AbstractModel>(id);
				if (model is not RelicModel relic)
				{
					LogForgeRestoreSkip(id, model == null ? "model is not registered" : $"model type is {model.GetType().FullName}");
					continue;
				}

				if (!HextechCatalog.IsHextechForgeRelic(relic))
				{
					LogForgeRestoreSkip(id, "model is no longer registered as a Hextech forge");
					continue;
				}

				options.Add(relic);
				if (options.Count >= requestedCount)
				{
					break;
				}
			}
			catch (Exception ex)
			{
				LogForgeRestoreSkip(id, $"{ex.GetType().Name}: {ex.Message}");
			}
		}

		if (options.Count == 0)
		{
			LogForgeRestoreSkip(ModelId.none, "no valid saved forge options remain");
			return false;
		}

		try
		{
			reward = new HextechForgeChoiceReward(options, player);
			return true;
		}
		catch (Exception ex)
		{
			LogForgeRestoreSkip(ModelId.none, $"failed to materialize restored options: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static RelicModel CreateMutableOption(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return ModelDb.GetById<RelicModel>(id).ToMutable();
	}

	private string GetForgeRewardIconPath()
	{
		RelicModel? firstOption = _options.FirstOrDefault();
		return firstOption != null && HextechCatalog.TryGetForgeRarity(firstOption, out HextechRarityTier rarity)
			? HextechAssets.GetForgeIconPath(rarity)
			: ImageHelper.GetImagePath("ui/reward_screen/reward_icon_relic.png");
	}

	private static void LogForgeRestoreSkip(ModelId id, string reason)
	{
		if (HextechRunLogBudget.TryConsume("rewards.forge-choice-restore-skip", 12))
		{
			Log.Warn($"[{ModInfo.Id}][ForgeChoiceReward] Saved forge option skipped: id={id} reason={reason}.");
		}
	}
}
