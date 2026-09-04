using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class IntegratedStrategyEndlessFinaleMusicController
{
	private const string PlayerName = "IntegratedStrategyEndlessFinaleMusic";
	private const string TrackPath = $"res://{ModInfo.ModId}/audio/music/endless_finale.ogg";
	private const float VolumeScale = 0.82f;

	private static readonly FieldInfo? RunMusicProxyField =
		IntegratedStrategyPrivateMembers.Field(typeof(NRunMusicController), "_proxy");

	private static AudioStreamPlayer? _player;
	private static bool _isPlaying;

	public static void Play()
	{
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
			Log.Warn($"{ModInfo.LogPrefix} Could not load endless finale music: {TrackPath}");
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

	public static void Stop(bool restoreGameMusic)
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

	private static AudioStream? LoadTrack()
	{
		return GD.Load<AudioStream>(TrackPath) ??
			ResourceLoader.Load<AudioStream>(TrackPath) ??
			AudioStreamOggVorbis.LoadFromFile(TrackPath);
	}

	private static void RefreshVolume()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		float bgmVolume = Math.Clamp(SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f, 0f, 1f);
		_player.VolumeLinear = Math.Clamp(bgmVolume * VolumeScale, 0f, 1f);
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

	private static void EnsurePlayer()
	{
		if (GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		_player = new AudioStreamPlayer
		{
			Name = PlayerName,
			Bus = "Master",
			ProcessMode = Node.ProcessModeEnum.Always
		};
		_player.Connect(AudioStreamPlayer.SignalName.Finished, Callable.From(OnTrackFinished));

		if (NGame.Instance == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not attach endless finale music player because NGame.Instance is null.");
			_player = null;
			return;
		}

		NGame.Instance.AddChild(_player);
	}

	private static void OnTrackFinished()
	{
		if (!_isPlaying || _player == null || !GodotObject.IsInstanceValid(_player) || _player.Stream == null)
		{
			return;
		}

		_player.Play();
	}
}
