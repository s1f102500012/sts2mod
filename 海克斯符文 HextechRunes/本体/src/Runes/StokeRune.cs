using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class StokeRune : HextechRelicBase
{
	public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
	{
		if (Owner == null || player != Owner || options.Any(static option => option.OptionId == StokeRestSiteOption.OptionIdValue))
		{
			return false;
		}

		options.Add(new StokeRestSiteOption(player));
		return true;
	}
}

internal sealed class StokeRestSiteOption : RestSiteOption
{
	public const string OptionIdValue = "STOKE";

	// 基类的 RestSiteOption.Icon 写死从 PreloadManager.Cache 取 res://images/ui/rest_site/option_stoke.png,
	// 而模组的 PCK 只能挂在 res://HextechRunes/ 下、无法提供该 base-game 命名空间的真实资源。
	// 图标改由 HextechAssetHooks 的 RestSiteOption.Icon 前缀补丁直接返回这里解析出的纹理(见 ResolveIcon),
	// 因此本选项不再声明任何需要预载的资源(避免预载器尝试加载不存在的路径并产生噪声)。
	private static Texture2D? _icon;

	public override string OptionId => OptionIdValue;

	public override LocString Description => IsEnabled
		? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
		: new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

	public override IEnumerable<string> AssetPaths => [];

#if STS2_104_OR_NEWER
	public override bool IsEnabled => CanRemoveCard;
#endif

	public StokeRestSiteOption(Player owner)
		: base(owner)
	{
#if !STS2_104_OR_NEWER
		IsEnabled = CanRemoveCard;
#endif
	}

	public override async Task<bool> OnSelect()
	{
		CardSelectorPrefs prefs = new(CardSelectorPrefs.RemoveSelectionPrompt, 1)
		{
			Cancelable = true,
			RequireManualConfirmation = true
		};

		CardModel? card = (await CardSelectCmd.FromDeckForRemoval(Owner, prefs)).FirstOrDefault();
		if (card == null)
		{
			return false;
		}

		await CardPileCmd.RemoveFromDeck(card);
		return true;
	}

	private static int GetRemovableCardCount(Player player)
	{
		return PileType.Deck.GetPile(player).Cards.Count(static card => card.IsRemovable);
	}

	private bool CanRemoveCard => GetRemovableCardCount(Owner) >= 1;

	/// <summary>
	/// 解析「添柴」休息室选项的图标:复用原版 Stoke 卡牌的立绘(真实磁盘资源,任何端、任何时机都能稳定加载,
	/// 不依赖会被房间切换卸载的 AssetCache 别名)。供 <see cref="HextechAssetHooks"/> 的 RestSiteOption.Icon 补丁调用。
	///
	/// 此前的实现把一张别名贴图塞进 <c>PreloadManager.Cache</c>,但该缓存会在进入休息室房间时按「本地玩家选项」
	/// 卸载未被引用的资源 —— 对非遗物持有方(联机另一端)而言这张别名被判为多余而清掉,导致取图返回 null、
	/// 渲染思考气泡时抛 <c>NotImplementedException</c>。该异常发生在 <c>RestSiteSynchronizer.ChooseOption</c>
	/// 的 <c>BeforePlayerOptionChosen</c> 阶段(早于把选项写入 RestSiteChoices / 移除已选项),会让一端中断而另一端跑完,
	/// 进而在离开休息室时校验和分叉(StateDivergence 踢人)。返回稳定纹理后该链条整体消除。
	/// </summary>
	internal static Texture2D? ResolveIcon()
	{
		if (_icon != null && GodotObject.IsInstanceValid(_icon))
		{
			return _icon;
		}

		try
		{
			Texture2D? portrait = ModelDb.Card<Stoke>().Portrait;
			if (portrait != null && GodotObject.IsInstanceValid(portrait))
			{
				_icon = portrait;
			}
		}
		catch
		{
			// 兜底:绝不让图标解析失败抛出 —— 一次 hover/选择渲染失败会中断同步并打断对局。
			// 失败时返回 null,补丁会放行原版逻辑(退化为修复前行为),不会比原来更糟。
		}

		return _icon;
	}
}
