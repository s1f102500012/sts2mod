using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen
{
	private static AudioStreamPlayer? RerollSfxPlayer;
	private static AudioStreamPlayer? SelectSfxPlayer;
	private static readonly Dictionary<string, AudioStream> SfxStreamCache = new();

	private void PlayRerollSfx()
	{
		PlaySfx(RerollButtonSfxPath, ref RerollSfxPlayer, "HextechRerollSfx", RerollButtonSfxVolumeScale);
	}

	private static void PlayButtonClickSfx()
	{
		SfxCmd.Play("event:/sfx/ui/clicks/ui_click");
	}

	private void PlayRuneSelectSfx(RelicModel relic)
	{
		if (!HextechCatalog.TryGetPlayerRuneRarity(relic, out HextechRarityTier rarity))
		{
			return;
		}

		PlaySfx(GetSelectSfxPath(rarity), ref SelectSfxPlayer, "HextechRuneSelectSfx", SelectSfxVolumeScale);
	}

	private static string GetSelectSfxPath(HextechRarityTier rarity)
	{
		return rarity switch
		{
			HextechRarityTier.Silver => SelectSilverSfxPath,
			HextechRarityTier.Gold => SelectGoldSfxPath,
			HextechRarityTier.Prismatic => SelectPrismaticSfxPath,
			_ => SelectSilverSfxPath
		};
	}

	private void PlaySfx(string path, ref AudioStreamPlayer? playerSlot, string playerName, float volumeScale)
	{
		AudioStream? stream = GetSfxStream(path);
		if (stream == null)
		{
			return;
		}

		AudioStreamPlayer? player = GetSfxPlayer(ref playerSlot, playerName);
		if (player == null)
		{
			return;
		}

		player.Stop();
		player.Stream = stream;
		player.VolumeLinear = Math.Clamp(GetSfxVolume() * volumeScale, 0f, 1f);
		player.Play();
	}

	private static AudioStream? GetSfxStream(string path)
	{
		if (SfxStreamCache.TryGetValue(path, out AudioStream? cachedStream))
		{
			return cachedStream;
		}

		AudioStream? stream = GD.Load<AudioStream>(path) ?? ResourceLoader.Load<AudioStream>(path);
		if (stream == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.PlaySfx: failed to load sfx path={path}");
			return null;
		}

		SfxStreamCache[path] = stream;
		return stream;
	}

	private AudioStreamPlayer? GetSfxPlayer(ref AudioStreamPlayer? playerSlot, string playerName)
	{
		if (GodotObject.IsInstanceValid(playerSlot))
		{
			return playerSlot;
		}

		AudioStreamPlayer player = new()
		{
			Name = playerName,
			Bus = "Master",
			ProcessMode = ProcessModeEnum.Always
		};

		Node? host = NGame.Instance;
		host ??= GetTree()?.Root;
		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SelectionScreen.PlaySfx: failed to attach audio player name={playerName}.");
			player.QueueFree();
			return null;
		}

		host.AddChild(player);
		playerSlot = player;
		return playerSlot;
	}

	private static float GetSfxVolume()
	{
		return Math.Clamp(SaveManager.Instance?.SettingsSave?.VolumeSfx ?? 1f, 0f, 1f);
	}
}
