namespace HextechRunes;

// 给不引用 HextechRunes.dll 的模组用（反射或 RitsuLib [ModInterop]）：签名只出现 BCL 与原版类型。
// 公开签名一经发布不再改动——调用方绑定的是完整参数列表，加参数就是 MissingMethodException。
// 新能力一律新增方法并递增 ApiVersion。
public static class HextechRunesInterop
{
	private static readonly object ExtraActProviderLock = new();
	private static readonly List<Func<IRunState, string?>> ExtraActProviders = [];

	/// <summary>
	/// 本类公开契约的版本。1 = RegisterPlayerRune、SetPlayerRunePoolLabel、RegisterConfigSectionTitle、RegisterExtraActProvider。
	/// </summary>
	public static int ApiVersion => 1;

	/// <summary>
	/// 注册外部玩家符文，符文类型只需继承 RelicModel，且 Rarity 必须是 RelicRarity.Starter。
	/// 必须在模组初始化阶段、共享遗物池首次枚举前调用。
	/// </summary>
	/// <param name="runeType">具体、封闭的 RelicModel 子类。</param>
	/// <param name="rarity">海克斯品级名：Silver / Gold / Prismatic（不区分大小写）。</param>
	/// <param name="flags">PlayerRuneFlags 名称，逗号分隔；null 或空串为 None。</param>
	/// <param name="characterPool">角色池名：Ironclad / Silent / Regent / Defect / Necrobinder；null 为通用。</param>
	/// <param name="characterOrder">角色池内排序。</param>
	/// <param name="tagKey">海克斯标签键；null 或空白为 COMPREHENSIVE。</param>
	/// <param name="assetModId">图标与配置菜单来源显示用的模组 ID；null 视为本体。</param>
	/// <param name="isAvailableForPlayer">可选的发放过滤；联机两端必须对同一输入给出相同结果。</param>
	/// <exception cref="ArgumentException">类型或名称参数无效；此时不产生任何登记。</exception>
	/// <exception cref="InvalidOperationException">模型池或 SavedProperty 注册窗口已经关闭。</exception>
	public static void RegisterPlayerRune(
		Type runeType,
		string rarity,
		string? flags,
		string? characterPool,
		int characterOrder,
		string? tagKey,
		string? assetModId,
		Func<Player, bool>? isAvailableForPlayer)
	{
		HextechRarityTier parsedRarity = ParseEnumName<HextechRarityTier>(rarity, nameof(rarity));
		PlayerRuneFlags parsedFlags = ParsePlayerRuneFlags(flags);
		PlayerRuneCharacterPool? parsedCharacterPool = string.IsNullOrWhiteSpace(characterPool)
			? null
			: ParseEnumName<PlayerRuneCharacterPool>(characterPool, nameof(characterPool));
		HextechRunesApi.RegisterPlayerRuneCore(
			runeType,
			typeof(RelicModel),
			parsedRarity,
			parsedFlags,
			parsedCharacterPool,
			characterOrder,
			string.IsNullOrWhiteSpace(tagKey) ? HextechPlayerRuneRegistry.DefaultTagKey : tagKey,
			assetModId,
			isAvailableForPlayer);
	}

	/// <summary>
	/// 指定符文在选择界面和配置菜单上显示的来源标签，文字取 relic_collection 表的 HEXTECH_POOL.&lt;poolKey&gt;。
	/// 可以选已有的键（GENERIC、IRONCLAD 等），也可以在自己模组的 relic_collection.json 里新增。
	/// 未指定时，带角色池的符文显示角色名，其余显示“通用”。只影响界面文字，不影响发放。
	/// </summary>
	/// <exception cref="ArgumentException">类型不是具体的 RelicModel，或 poolKey 为空白。</exception>
	public static void SetPlayerRunePoolLabel(Type runeType, string poolKey)
	{
		HextechRunesApi.ValidateConcreteModelType(runeType, typeof(RelicModel), nameof(runeType), "Player rune");
		HextechExternalContentRegistry.SetPlayerRunePoolLabel(runeType, RequireKey(poolKey, nameof(poolKey)));
	}

	/// <summary>
	/// 指定配置菜单里该模组内容（符文与锻造器）的分组标题，文字取 relic_collection 表的 titleKey。
	/// assetModId 与注册内容时传入的一致。未指定时显示“外部模组：&lt;assetModId&gt;”。只影响界面文字。
	/// </summary>
	/// <exception cref="ArgumentException">assetModId 或 titleKey 为空白。</exception>
	public static void RegisterConfigSectionTitle(string assetModId, string titleKey)
	{
		HextechExternalContentRegistry.RegisterConfigSectionTitle(
			RequireKey(assetModId, nameof(assetModId)),
			RequireKey(titleKey, nameof(titleKey)));
	}

	private static string RequireKey(string? value, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException($"{parameterName} must not be empty.", parameterName);
		}

		return value.Trim();
	}

	// 只认枚举名：数字字符串会让调用方悄悄绑定到内部序号。
	internal static TEnum ParseEnumName<TEnum>(string? value, string parameterName)
		where TEnum : struct, Enum
	{
		string trimmed = value?.Trim() ?? string.Empty;
		foreach (string name in Enum.GetNames<TEnum>())
		{
			if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
			{
				return Enum.Parse<TEnum>(name);
			}
		}

		throw new ArgumentException(
			$"Unknown {typeof(TEnum).Name} name '{value}'. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}.",
			parameterName);
	}

	internal static PlayerRuneFlags ParsePlayerRuneFlags(string? flags)
	{
		PlayerRuneFlags result = PlayerRuneFlags.None;
		if (string.IsNullOrWhiteSpace(flags))
		{
			return result;
		}

		foreach (string part in flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			result |= ParseEnumName<PlayerRuneFlags>(part, nameof(flags));
		}

		return result;
	}

	public static void RegisterExtraActProvider(Func<IRunState, string?> provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		lock (ExtraActProviderLock)
		{
			if (!ExtraActProviders.Contains(provider))
			{
				ExtraActProviders.Add(provider);
			}
		}
	}

	internal static string? GetCurrentExtraActId(IRunState runState)
	{
		Func<IRunState, string?>[] providers;
		lock (ExtraActProviderLock)
		{
			providers = ExtraActProviders.ToArray();
		}

		foreach (Func<IRunState, string?> provider in providers)
		{
			try
			{
				string? stageId = provider(runState);
				if (!string.IsNullOrWhiteSpace(stageId))
				{
					return stageId.Trim();
				}
			}
			catch (Exception ex)
			{
				if (HextechRunLogBudget.TryConsume("compat.extra-act-provider", 3))
				{
					Log.Warn($"[{ModInfo.Id}][Mayhem] Extra act provider failed and was ignored: {ex.Message}");
				}
			}
		}

		return null;
	}
}
