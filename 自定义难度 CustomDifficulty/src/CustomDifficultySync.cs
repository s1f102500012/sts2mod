using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace CustomDifficulty;

internal static class CustomDifficultySync
{
	private static INetGameService? _registeredService;

	public static NetGameType CurrentGameType => _registeredService?.Type ?? NetGameType.None;

	public static bool CanLocalEdit
	{
		get
		{
			return CurrentGameType is NetGameType.None or NetGameType.Singleplayer or NetGameType.Host;
		}
	}

	public static void Register(INetGameService netService)
	{
		if (ReferenceEquals(_registeredService, netService))
		{
			return;
		}

		Unregister();
		netService.RegisterMessageHandler<CustomDifficultySettingsMessage>(OnSettingsReceived);
		_registeredService = netService;
		Log.Info($"[{ModInfo.Id}] Sync registered: {netService.Type} netId={netService.NetId}.");
	}

	public static void Unregister()
	{
		if (_registeredService == null)
		{
			return;
		}

		try
		{
			_registeredService.UnregisterMessageHandler<CustomDifficultySettingsMessage>(OnSettingsReceived);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to unregister sync handler: {ex.Message}");
		}
		finally
		{
			_registeredService = null;
		}
	}

	public static void BroadcastSettings()
	{
		if (_registeredService?.Type != NetGameType.Host)
		{
			return;
		}

		try
		{
			_registeredService.SendMessage(CreateMessage());
			Log.Debug($"[{ModInfo.Id}] Broadcast settings hp={CustomDifficultySettings.MonsterHpTicks} attack={CustomDifficultySettings.MonsterAttackTicks}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to broadcast settings: {ex.Message}");
		}
	}

	public static void RequestHostSettings()
	{
		if (_registeredService?.Type != NetGameType.Client)
		{
			return;
		}

		try
		{
			_registeredService.SendMessage(CreateMessage());
			Log.Debug($"[{ModInfo.Id}] Requested host settings.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to request host settings: {ex.Message}");
		}
	}

	private static CustomDifficultySettingsMessage CreateMessage()
	{
		return new CustomDifficultySettingsMessage
		{
			HpTicks = (byte)CustomDifficultySettings.MonsterHpTicks,
			AttackTicks = (byte)CustomDifficultySettings.MonsterAttackTicks,
			DifficultyMode = (byte)CustomDifficultySettings.Mode,
			HpDeltaPercentPerRoom = CustomDifficultySettings.HpDeltaPercentPerRoom,
			AttackDeltaPercentPerRoom = CustomDifficultySettings.AttackDeltaPercentPerRoom
		};
	}

	private static void OnSettingsReceived(CustomDifficultySettingsMessage message, ulong senderId)
	{
		NetGameType type = _registeredService?.Type ?? NetGameType.None;
		if (type == NetGameType.Client)
		{
			CustomDifficultySettings.SetRemote(
				message.HpTicks,
				message.AttackTicks,
				CustomDifficultySettings.NormalizeMode(message.DifficultyMode),
				message.HpDeltaPercentPerRoom,
				message.AttackDeltaPercentPerRoom);
			Log.Info($"[{ModInfo.Id}] Received host settings from {senderId}: mode={message.DifficultyMode} hp={message.HpTicks} attack={message.AttackTicks} hpDelta={message.HpDeltaPercentPerRoom} attackDelta={message.AttackDeltaPercentPerRoom}.");
			return;
		}

		if (type == NetGameType.Host)
		{
			Log.Debug($"[{ModInfo.Id}] Received client sync request from {senderId}; resending host settings.");
			BroadcastSettings();
		}
	}
}
