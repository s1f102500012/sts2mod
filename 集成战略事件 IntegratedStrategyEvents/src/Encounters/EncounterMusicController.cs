using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace IntegratedStrategyEvents.Encounters;

internal class EncounterMusicController
{
	private static readonly FieldInfo? RunMusicProxyField =
		IntegratedStrategyPrivateMembers.Field(typeof(NRunMusicController), "_proxy");

	private readonly string _playerName;
	private readonly string _trackPath;
	private readonly float _volumeScale;
	private readonly string _logName;
	private readonly Predicate<EncounterModel> _isTargetEncounter;

	private AudioStreamPlayer? _player;
	private bool _isPlaying;
	private bool _isInstalled;

	public EncounterMusicController(
		string playerName,
		string trackPath,
		float volumeScale,
		string logName,
		Predicate<EncounterModel> isTargetEncounter)
	{
		_playerName = playerName;
		_trackPath = trackPath;
		_volumeScale = volumeScale;
		_logName = logName;
		_isTargetEncounter = isTargetEncounter;
	}

	public void Play()
	{
		PlayMusic();
	}

	public void Stop(bool restoreGameMusic)
	{
		StopMusic(restoreGameMusic);
	}

	protected void PlayMusic()
	{
		Install();
		EnsurePlayer();
		if (_player == null)
		{
			return;
		}

		StopBaseMusicOnly();
		if (_isPlaying && _player.IsPlaying())
		{
			RefreshVolume();
			return;
		}

		AudioStream? stream = LoadTrack();
		if (stream == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not load {_logName} music: {_trackPath}");
			return;
		}

		if (stream is AudioStreamOggVorbis ogg)
		{
			ogg.Loop = true;
		}

		_player.Stream = stream;
		RefreshVolume();
		_player.Play();
		_isPlaying = true;
	}

	protected void StopMusic(bool restoreGameMusic)
	{
		bool wasPlaying = _isPlaying;
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			if (_player.IsPlaying())
			{
				_player.Stop();
			}

			_player.Stream = null;
		}

		_isPlaying = false;
		if (!restoreGameMusic || !wasPlaying)
		{
			return;
		}

		NRunMusicController.Instance?.UpdateMusic();
		NRunMusicController.Instance?.UpdateTrack();
		NRunMusicController.Instance?.UpdateAmbience();
	}

	private void Install()
	{
		if (_isInstalled)
		{
			return;
		}

		CombatManager.Instance.CombatEnded += OnCombatEnded;
		_isInstalled = true;
	}

	private void OnCombatEnded(CombatRoom room)
	{
		if (_isTargetEncounter(room.Encounter))
		{
			StopMusic(restoreGameMusic: true);
		}
	}

	private AudioStream? LoadTrack()
	{
		return GD.Load<AudioStream>(_trackPath) ??
			ResourceLoader.Load<AudioStream>(_trackPath) ??
			AudioStreamOggVorbis.LoadFromFile(_trackPath);
	}

	private void RefreshVolume()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		float bgmVolume = Math.Clamp(SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f, 0f, 1f);
		_player.VolumeLinear = Math.Clamp(bgmVolume * _volumeScale, 0f, 1f);
	}

	private static void StopBaseMusicOnly()
	{
		if (NRunMusicController.Instance == null)
		{
			return;
		}

		if (RunMusicProxyField?.GetValue(NRunMusicController.Instance) is not Node proxy ||
			!GodotObject.IsInstanceValid(proxy))
		{
			return;
		}

		proxy.Call("stop_music");
	}

	private void EnsurePlayer()
	{
		if (GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		_player = new AudioStreamPlayer
		{
			Name = _playerName,
			Bus = "Master",
			ProcessMode = Node.ProcessModeEnum.Always
		};
		_player.Connect(AudioStreamPlayer.SignalName.Finished, Callable.From(OnTrackFinished));

		if (NGame.Instance == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not attach {_logName} music player because NGame.Instance is null.");
			_player = null;
			return;
		}

		NGame.Instance.AddChild(_player);
	}

	private void OnTrackFinished()
	{
		if (!_isPlaying || _player == null || !GodotObject.IsInstanceValid(_player) || _player.Stream == null)
		{
			return;
		}

		_player.Play();
	}
}
