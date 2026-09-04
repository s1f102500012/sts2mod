namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleFinaleMusicCoordinator
{
	public static void StopForRunReset()
	{
		IntegratedStrategyPresentation.Run(() => IntegratedStrategyEndlessFinaleMusicController.Stop(restoreGameMusic: false), "stop finale music");
	}

	public static void PlayForEnteredRoom(EndlessFinaleSession session)
	{
		if (session.Kind == SpecialFinaleKind.EndlessFinale)
		{
			IntegratedStrategyPresentation.Run(IntegratedStrategyEndlessFinaleMusicController.Play, "play finale music");
		}
	}

	public static void PlayAfterFinaleEntry(SpecialFinaleKind finaleKind)
	{
		if (finaleKind == SpecialFinaleKind.EndlessFinale)
		{
			IntegratedStrategyPresentation.Run(IntegratedStrategyEndlessFinaleMusicController.Play, "play finale music");
		}
	}

	public static void StopBeforeArchitectHandoff(EndlessFinaleSession session)
	{
		if (session.Kind == SpecialFinaleKind.EndlessFinale)
		{
			IntegratedStrategyPresentation.Run(() => IntegratedStrategyEndlessFinaleMusicController.Stop(restoreGameMusic: false), "stop finale music");
		}
	}
}
