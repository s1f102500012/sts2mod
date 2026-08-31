using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunesSponsorPack;

public sealed class SponsorCompositeEnchantment : EnchantmentModel
{
	private List<EnchantmentModel> _innerEnchantments = [];
	private List<EnchantmentModel> _subscribedInnerEnchantments = [];

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private string? SavedEnchantmentsJson
	{
		get => _innerEnchantments.Count == 0
			? null
			: JsonSerializer.Serialize(_innerEnchantments.Select(static enchantment => enchantment.ToSerializable()).ToArray());
		set
		{
			UnsubscribeFromInnerEnchantments();
			_innerEnchantments = [];
			if (string.IsNullOrWhiteSpace(value))
			{
				Amount = 0;
				BuiltInRepeatableEnchantments.DebugLog("Save", "Loaded empty composite enchantment payload.");
				return;
			}

			SerializableEnchantment[]? serialized = JsonSerializer.Deserialize<SerializableEnchantment[]>(value);
			if (serialized == null)
			{
				Amount = 0;
				BuiltInRepeatableEnchantments.DebugLog("Save", "Composite enchantment payload deserialized to null.");
				return;
			}

			foreach (SerializableEnchantment item in serialized)
			{
				_innerEnchantments.Add(EnchantmentModel.FromSerializable(item));
			}

			Amount = _innerEnchantments.Count;
			RefreshCompositeStatus();
			BuiltInRepeatableEnchantments.DebugLog("Save", $"Loaded {_innerEnchantments.Count} inner enchantments into composite: {DescribeInnerEnchantments()}.");
		}
	}

	public override bool HasExtraCardText => false;

	public override bool ShowAmount
	{
		get
		{
			EnsureInnerBindings();
			if (_innerEnchantments.Count > 1)
			{
				return true;
			}

			return GetLeadEnchantment()?.ShowAmount ?? false;
		}
	}

	public override int DisplayAmount
	{
		get
		{
			EnsureInnerBindings();
			if (_innerEnchantments.Count > 1)
			{
				return _innerEnchantments.Count;
			}

			return GetLeadEnchantment()?.DisplayAmount ?? 0;
		}
	}

	public override bool ShouldStartAtBottomOfDrawPile
	{
		get
		{
			EnsureInnerBindings();
			if (HasCard && Card.Keywords.Contains(CardKeyword.Innate))
			{
				return false;
			}

			return _innerEnchantments.Any(static enchantment => enchantment.ShouldStartAtBottomOfDrawPile);
		}
	}

	public override bool ShouldGlowGold
	{
		get
		{
			EnsureInnerBindings();
			return _innerEnchantments.Any(static enchantment => enchantment.ShouldGlowGold);
		}
	}

	public override bool ShouldGlowRed
	{
		get
		{
			EnsureInnerBindings();
			return _innerEnchantments.Any(static enchantment => enchantment.ShouldGlowRed);
		}
	}

	protected override IEnumerable<IHoverTip> ExtraHoverTips
	{
		get
		{
			EnsureInnerBindings();
			return _innerEnchantments.SelectMany(static enchantment => enchantment.HoverTips).ToList();
		}
	}

	public IReadOnlyList<EnchantmentModel> InnerEnchantments
	{
		get
		{
			EnsureInnerBindings();
			return _innerEnchantments;
		}
	}

	public override bool CanEnchant(CardModel card)
	{
		return false;
	}

	public EnchantmentModel? GetLeadEnchantment()
	{
		EnsureInnerBindings();
		return _innerEnchantments.LastOrDefault();
	}

	public bool ContainsEnchantmentType(Type enchantmentType)
	{
		EnsureInnerBindings();
		return _innerEnchantments.Any(enchantment => enchantment.GetType() == enchantmentType);
	}

	public EnchantmentModel? FindEnchantment(Type enchantmentType)
	{
		EnsureInnerBindings();
		return _innerEnchantments.FirstOrDefault(enchantment => enchantment.GetType() == enchantmentType);
	}

	public EnchantmentModel ImportExistingEnchantment(EnchantmentModel enchantment)
	{
		AssertMutable();
		EnsureCompositeCard();
		if (!enchantment.HasCard)
		{
			enchantment.ApplyInternal(Card, enchantment.Amount);
		}
		else if (!ReferenceEquals(enchantment.Card, Card))
		{
			BuiltInRepeatableEnchantments.DebugLog("Composite", $"Rebinding imported enchantment {enchantment.Id.Entry} from {BuiltInRepeatableEnchantments.DescribeCard(enchantment.Card)} to {BuiltInRepeatableEnchantments.DescribeCard(Card)}.");
			enchantment.ClearInternal();
			enchantment.ApplyInternal(Card, enchantment.Amount);
		}

		_innerEnchantments.Add(enchantment);
		SubscribeToInnerEnchantment(enchantment);
		Amount = _innerEnchantments.Count;
		RefreshCompositeStatus();
		BuiltInRepeatableEnchantments.DebugLog("Composite", $"Imported existing enchantment {enchantment.Id.Entry} into {BuiltInRepeatableEnchantments.DescribeCard(Card)}. Current={DescribeInnerEnchantments()}.");
		return enchantment;
	}

	public EnchantmentModel AddOrStackEnchantment(EnchantmentModel enchantment, decimal amount, bool refreshConsumedStacks)
	{
		AssertMutable();
		EnsureCompositeCard();
		EnsureInnerBindings();

		EnchantmentModel? existing = FindEnchantment(enchantment.GetType());
		if (existing != null)
		{
			BuiltInRepeatableEnchantments.DebugLog("Composite", $"Stacking {enchantment.Id.Entry} on {BuiltInRepeatableEnchantments.DescribeCard(Card)} by {amount}. Existing amount={existing.Amount}, status={existing.Status}.");
			existing.Amount += (int)amount;
			if (refreshConsumedStacks && existing.Status == EnchantmentStatus.Disabled)
			{
				existing.Status = EnchantmentStatus.Normal;
			}

			existing.RecalculateValues();
			Card.DynamicVars.RecalculateForUpgradeOrEnchant();
			RefreshCompositeStatus();
			return existing;
		}

		enchantment.ApplyInternal(Card, amount);
		_innerEnchantments.Add(enchantment);
		SubscribeToInnerEnchantment(enchantment);
		Amount = _innerEnchantments.Count;
		enchantment.ModifyCard();
		RefreshCompositeStatus();
		BuiltInRepeatableEnchantments.DebugLog("Composite", $"Added new inner enchantment {enchantment.Id.Entry} to {BuiltInRepeatableEnchantments.DescribeCard(Card)}. Current={DescribeInnerEnchantments()}.");
		return enchantment;
	}

	public IEnumerable<string> GetVisibleExtraCardTextLines()
	{
		EnsureInnerBindings();
		HashSet<string> seen = new(StringComparer.Ordinal);
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			string? text = enchantment.DynamicDescription.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
			{
				yield return "[purple]" + text + "[/purple]";
			}
		}
	}

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		_innerEnchantments = _innerEnchantments
			.Select(static enchantment => (EnchantmentModel)enchantment.ClonePreservingMutability())
			.ToList();
		_subscribedInnerEnchantments = [];
	}

	protected override void OnEnchant()
	{
		EnsureInnerBindings();
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			enchantment.ModifyCard();
		}

		Amount = _innerEnchantments.Count;
		RefreshCompositeStatus();
	}

	public override void RecalculateValues()
	{
		EnsureInnerBindings();
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			enchantment.RecalculateValues();
		}

		Amount = _innerEnchantments.Count;
		Card?.DynamicVars.RecalculateForUpgradeOrEnchant();
		RefreshCompositeStatus();
	}

	public override decimal EnchantBlockAdditive(decimal originalBlock)
	{
		EnsureInnerBindings();
		return CalculateFinalBlock(originalBlock) - originalBlock;
	}

	public override decimal EnchantBlockMultiplicative(decimal originalBlock)
	{
		return 1m;
	}

	public override decimal EnchantDamageAdditive(decimal originalDamage, ValueProp props)
	{
		EnsureInnerBindings();
		return CalculateFinalDamage(originalDamage, props) - originalDamage;
	}

	public override decimal EnchantDamageMultiplicative(decimal originalDamage, ValueProp props)
	{
		return 1m;
	}

	public override int EnchantPlayCount(int originalPlayCount)
	{
		EnsureInnerBindings();
		int current = originalPlayCount;
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			current = enchantment.EnchantPlayCount(current);
		}

		return current;
	}

	public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
	{
		EnsureInnerBindings();
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			await enchantment.OnPlay(choiceContext, cardPlay);
			enchantment.InvokeExecutionFinished();
		}

		RefreshCompositeStatus();
	}

	private decimal CalculateFinalBlock(decimal originalBlock)
	{
		decimal current = originalBlock;
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			current += enchantment.EnchantBlockAdditive(current);
			current *= enchantment.EnchantBlockMultiplicative(current);
		}

		return current;
	}

	private decimal CalculateFinalDamage(decimal originalDamage, ValueProp props)
	{
		decimal current = originalDamage;
		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			current += enchantment.EnchantDamageAdditive(current, props);
			current *= enchantment.EnchantDamageMultiplicative(current, props);
		}

		return current;
	}

	private void EnsureCompositeCard()
	{
		if (!HasCard)
		{
			throw new InvalidOperationException("Composite enchantment must be attached to a card before it can manage inner enchantments.");
		}
	}

	private void EnsureInnerBindings()
	{
		if (!HasCard)
		{
			return;
		}

		foreach (EnchantmentModel enchantment in _innerEnchantments)
		{
			if (!enchantment.HasCard || !ReferenceEquals(enchantment.Card, Card))
			{
				if (enchantment.HasCard)
				{
					enchantment.ClearInternal();
				}

				enchantment.ApplyInternal(Card, enchantment.Amount);
			}

			SubscribeToInnerEnchantment(enchantment);
		}
	}

	private void SubscribeToInnerEnchantment(EnchantmentModel enchantment)
	{
		if (_subscribedInnerEnchantments.Contains(enchantment))
		{
			return;
		}

		enchantment.StatusChanged += OnInnerEnchantmentStatusChanged;
		_subscribedInnerEnchantments.Add(enchantment);
	}

	private void UnsubscribeFromInnerEnchantments()
	{
		foreach (EnchantmentModel enchantment in _subscribedInnerEnchantments)
		{
			enchantment.StatusChanged -= OnInnerEnchantmentStatusChanged;
		}

		_subscribedInnerEnchantments.Clear();
	}

	private void OnInnerEnchantmentStatusChanged()
	{
		RefreshCompositeStatus();
	}

	internal void RefreshCompositeStatus()
	{
		Status = _innerEnchantments.Any(static enchantment => enchantment.Status == EnchantmentStatus.Normal)
			? EnchantmentStatus.Normal
			: EnchantmentStatus.Disabled;
	}

	private string DescribeInnerEnchantments()
	{
		if (_innerEnchantments.Count == 0)
		{
			return "<none>";
		}

		return string.Join(", ", _innerEnchantments.Select(static enchantment => $"{enchantment.Id.Entry}x{enchantment.Amount}[{enchantment.Status}]"));
	}
}
