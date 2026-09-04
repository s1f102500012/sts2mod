using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace StS1Act4;

internal static class Act4MusicController
{
	private const string MapTrackPath = "res://StS1Act4/extracted/STS_Act4_BGM_v2.ogg";

	private const string HeartTrackPath = "res://StS1Act4/extracted/STS_Boss4_v6.ogg";

	private static readonly FieldInfo ProxyField = typeof(NRunMusicController).GetField("_proxy", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NRunMusicController._proxy.");

	private static AudioStreamPlayer? _player;

	private static string? _currentTrackPath;

	private static bool _restoreOriginalMusicForAct4;

	public static void Install()
	{
		RunManager.Instance.ActEntered += OnActEntered;
		RunManager.Instance.RoomEntered += OnRoomEntered;
		CombatManager.Instance.CombatEnded += OnCombatEnded;
	}

	private static void OnActEntered()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null)
		{
			return;
		}

		if (state.Act.Id == MegaCrit.Sts2.Core.Models.ModelDb.GetId<Sts1Act4>())
		{
			_restoreOriginalMusicForAct4 = false;
			PlayTrackForCurrentRoom(state);
		}
		else
		{
			StopAndRestoreGameMusic(clearAct4Override: true);
		}
	}

	private static void OnRoomEntered()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null)
		{
			return;
		}

		if (state.Act.Id != MegaCrit.Sts2.Core.Models.ModelDb.GetId<Sts1Act4>())
		{
			StopAndRestoreGameMusic(clearAct4Override: true);
			return;
		}

		if (_restoreOriginalMusicForAct4)
		{
			StopAndRestoreGameMusic(clearAct4Override: false);
			return;
		}

		PlayTrackForCurrentRoom(state);
	}

	private static void OnCombatEnded(CombatRoom room)
	{
		if (room.Encounter.Id != MegaCrit.Sts2.Core.Models.ModelDb.GetId<CorruptHeartEncounter>())
		{
			return;
		}

		_restoreOriginalMusicForAct4 = true;
		StopAndRestoreGameMusic(clearAct4Override: false);
	}

	private static void PlayTrackForCurrentRoom(RunState state)
	{
		if (state.CurrentRoom is CombatRoom combatRoom
			&& combatRoom.Encounter.Id == MegaCrit.Sts2.Core.Models.ModelDb.GetId<CorruptHeartEncounter>()
			&& !combatRoom.IsPreFinished)
		{
			Play(HeartTrackPath);
			return;
		}

		Play(MapTrackPath);
	}

	private static void Play(string trackPath)
	{
		if (_currentTrackPath == trackPath && _player != null && _player.IsPlaying())
		{
			return;
		}

		EnsurePlayer();
		_currentTrackPath = trackPath;
		StopBaseMusicOnly();
		_player!.Stream = AudioStreamOggVorbis.LoadFromFile(trackPath);
		_player.VolumeDb = -4f;
		_player.Play();
	}

	private static void StopAndRestoreGameMusic(bool clearAct4Override)
	{
		if (_player != null && _player.IsPlaying())
		{
			_player.Stop();
		}

		_currentTrackPath = null;
		if (clearAct4Override)
		{
			_restoreOriginalMusicForAct4 = false;
		}

		NRunMusicController.Instance?.UpdateMusic();
		NRunMusicController.Instance?.UpdateTrack();
		NRunMusicController.Instance?.UpdateAmbience();
	}

	private static void StopBaseMusicOnly()
	{
		if (NRunMusicController.Instance == null)
		{
			return;
		}

		if (ProxyField.GetValue(NRunMusicController.Instance) is not Node proxy || !GodotObject.IsInstanceValid(proxy))
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
			Name = "StS1Act4Music",
			Bus = "Master"
		};
		_player.Connect(AudioStreamPlayer.SignalName.Finished, Callable.From(OnTrackFinished));
		NGame.Instance.AddChild(_player);
	}

	private static void OnTrackFinished()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player) || _player.Stream == null || string.IsNullOrEmpty(_currentTrackPath))
		{
			return;
		}

		_player.Play();
	}
}
