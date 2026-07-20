using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace WoLuGuo;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string TargetGameVersion = "0.107.1";
	private const string HarmonyId = "Natsuki.WoLuGuo";
	private const string LeaveOptionTextKey = "WOLUGUO.leave";
	private const string LeaveTitleKey = "WOLUGUO.leave.title";
	private const string LeaveDescriptionKey = "WOLUGUO.leave.description";

	private static readonly MethodInfo SetEventFinishedMethod = RequireMethod(typeof(EventModel), "SetEventFinished", BindingFlags.Instance | BindingFlags.NonPublic, typeof(LocString));

	private static Harmony? _harmony;
	private static bool _hooksInstalled;

	public static void Initialize()
	{
		InstallHooks();
		Log.Info($"[WoLuGuo] Loaded for Slay the Spire 2 {TargetGameVersion}.");
	}

	private static void InstallHooks()
	{
		if (_hooksInstalled)
		{
			return;
		}

		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		harmony.Patch(
			RequireMethod(typeof(EventModel), "SetEventState", BindingFlags.Instance | BindingFlags.NonPublic, typeof(LocString), typeof(IEnumerable<EventOption>)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(SetEventStatePrefix))
			{
				priority = Priority.Last
			});
		_hooksInstalled = true;
	}

	private static void SetEventStatePrefix(EventModel __instance, ref IEnumerable<EventOption> eventOptions)
	{
		eventOptions = BuildEventOptions(__instance, eventOptions);
	}

	private static IEnumerable<EventOption> BuildEventOptions(EventModel eventModel, IEnumerable<EventOption> eventOptions)
	{
		List<EventOption> options = eventOptions.ToList();
		if (!ShouldAddLeaveOption(eventModel, options))
		{
			return options;
		}

		options.Add(new EventOption(
			eventModel,
			() => LeaveEvent(eventModel),
			CreateLeaveTitle(),
			CreateLeaveDescription(),
			LeaveOptionTextKey,
			Array.Empty<IHoverTip>()));

		return options;
	}

	private static bool ShouldAddLeaveOption(EventModel eventModel, List<EventOption> options)
	{
		if (eventModel.Id.Entry == "THE_ARCHITECT")
		{
			return false;
		}

		if (eventModel.Description != null)
		{
			return false;
		}

		if (options.Count == 0)
		{
			return false;
		}

		return !options.Any(option => option.IsProceed || option.TextKey == LeaveOptionTextKey);
	}

	private static Task LeaveEvent(EventModel eventModel)
	{
		if (!eventModel.IsFinished)
		{
			SetEventFinishedMethod.Invoke(eventModel, new object[] { CreateLeaveDescription() });
		}

		if (LocalContext.IsMine(eventModel) && eventModel.Node != null)
		{
			return NEventRoom.Proceed();
		}

		return Task.CompletedTask;
	}

	private static LocString CreateLeaveTitle()
	{
		return new LocString("events", LeaveTitleKey);
	}

	private static LocString CreateLeaveDescription()
	{
		return new LocString("events", LeaveDescriptionKey);
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		MethodInfo? method = type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
		}

		return method;
	}
}
