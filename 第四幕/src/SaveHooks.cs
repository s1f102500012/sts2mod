using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class SaveHooks
{
	private static Hook? _roomSetFromSaveHook;

	private delegate RoomSet OrigRoomSetFromSave(SerializableRoomSet save);

	public static void Install()
	{
		MethodInfo fromSave = RequireMethod(typeof(RoomSet), nameof(RoomSet.FromSave), BindingFlags.Public | BindingFlags.Static, typeof(SerializableRoomSet));
		_roomSetFromSaveHook = new Hook(fromSave, RoomSetFromSaveDetour);
	}

	private static RoomSet RoomSetFromSaveDetour(OrigRoomSetFromSave orig, SerializableRoomSet save)
	{
		save.EventIds ??= new List<ModelId>();
		save.NormalEncounterIds ??= new List<ModelId>();
		save.EliteEncounterIds ??= new List<ModelId>();
		return orig(save);
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameterTypes)
	{
		return type.GetMethod(name, flags, binder: null, parameterTypes, modifiers: null)
			?? throw new InvalidOperationException($"Could not find method {type.FullName}.{name}.");
	}
}
