using Godot;

namespace HextechRunes;

internal sealed partial class HextechControllerOverlay : Control
{
	public Action? CancelRequested { get; set; }
	public Control? InitialFocus { get; set; }
	private bool _controllerNavigationActivated;

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!IsVisibleInTree())
		{
			return;
		}

		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			GetViewport()?.SetInputAsHandled();
			CancelRequested?.Invoke();
			return;
		}

		if (_controllerNavigationActivated || !HextechControllerInput.IsIntentional(inputEvent))
		{
			return;
		}

		_controllerNavigationActivated = true;
		if (InitialFocus != null && GodotObject.IsInstanceValid(InitialFocus) && InitialFocus.IsInsideTree())
		{
			InitialFocus.GrabFocus();
			GetViewport()?.SetInputAsHandled();
		}
	}
}

internal static class HextechControllerInput
{
	private const float JoypadMotionThreshold = 0.5f;

	internal static bool IsIntentional(InputEvent inputEvent)
	{
		return inputEvent switch
		{
			InputEventJoypadButton button => button.Pressed,
			InputEventJoypadMotion motion => MathF.Abs(motion.AxisValue) >= JoypadMotionThreshold,
			_ => false
		};
	}
}
