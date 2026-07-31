using HarmonyLib;
using System.Runtime.CompilerServices;

namespace UniversalDominionSword.HarmonyTests;

internal static class Program
{
	private const string HarmonyId =
		"Natsuki.UniversalDominionSword.AdversarialTests";

	private const int SubjectPatchPriority = 10_000;

	private const int EarlierIndependentPatchPriority =
		SubjectPatchPriority + Priority.First;

	public static async Task<int> Main()
	{
		try
		{
			Harmony harmony = new(HarmonyId);
			harmony.PatchAll(typeof(Program).Assembly);

			MethodGuardsCannotObserveRawConvergence();
			AddPrefixPostfixFinalizerCombinationConverges();
			await SkippedOriginalStillRunsCapturePrefix();
			await HighPrioritySkipIsObservedAndCompleted();
			await SkipWithoutTaskUsesFalseFallback();
			await OriginalFalseCoordinatesCompletion();
			await PostfixPseudoSuccessCoordinatesCompletion();
			await InterceptedAddDoesNotAwaitStabilization();
			await SkippedAddWithoutTaskUsesCompletedFallback();
			await ConcurrentChecksInvokeEndOnce();
			await ReentrantEndHookDoesNotAwaitItself();
			await SkippedEndBodyCanRetry();
			await IncompleteEndAttemptIsNotRepeated();
			await OriginalExceptionIsNotSwallowed();

			Console.WriteLine("14/14 adversarial Harmony tests passed.");
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine($"FAIL {exception}");
			return 1;
		}
		finally
		{
			new Harmony(HarmonyId).UnpatchAll(HarmonyId);
		}
	}

	private static void MethodGuardsCannotObserveRawConvergence()
	{
		FakeCreature creature = new();

		RawConverger.Converge(creature);

		Assert.False(creature.Attached);
		Assert.False(creature.NodePresent);
		Assert.False(creature.MoveActive);
		Assert.Equal(0, creature.Hp);
		Assert.Equal(0, creature.Powers.Count);
		Assert.Equal(0, GuardCounters.HpSetter);
		Assert.Equal(0, GuardCounters.Remove);
		Assert.Equal(0, GuardCounters.Kill);
	}

	private static void AddPrefixPostfixFinalizerCombinationConverges()
	{
		FakeCreature creature = new()
		{
			Erased = true,
			Attached = false,
			NodePresent = false,
			MoveActive = false,
			Hp = 0
		};

		creature.Add();

		Assert.Equal(0, GuardCounters.AddPrefix);
		Assert.Equal(1, GuardCounters.AddPostfix);
		Assert.False(creature.Attached);
		Assert.False(creature.NodePresent);
		Assert.False(creature.MoveActive);
		Assert.Equal(0, creature.Hp);
	}

	private static async Task HighPrioritySkipIsObservedAndCompleted()
	{
		ResetCompletionScenario(HighPrioritySkipMode.AssignFalseResult);
		FakeCombatManager manager = new()
		{
			CanComplete = true
		};

		bool result = await manager.CheckWinCondition();

		Assert.True(result);
		Assert.False(manager.RawInProgress);
		Assert.Equal(0, manager.OriginalCalls);
		Assert.Equal(1, manager.EndCalls);
		Assert.Equal(1, FakeCompletionCoordinator.FinalizerCalls);
		Assert.Equal(1, FakeCompletionCoordinator.SkippedObservations);
		Assert.Equal(0, FakeCompletionCoordinator.RunOriginalObservations);
	}

	private static async Task SkippedOriginalStillRunsCapturePrefix()
	{
		ResetCompletionScenario(HighPrioritySkipMode.WithoutResult);
		FakeCombatManager manager = new();

		await manager.CheckWinCondition();

		Assert.Equal(1, FakeCompletionCoordinator.CapturePrefixCalls);
		Assert.Equal(1, FakeCompletionCoordinator.NonNullCaptureStates);
		Assert.Same(
			manager,
			FakeCompletionCoordinator.LastCapturedManager
				?? throw new InvalidOperationException(
					"Expected a captured manager."));
	}

	private static async Task SkipWithoutTaskUsesFalseFallback()
	{
		ResetCompletionScenario(HighPrioritySkipMode.WithoutResult);
		FakeCombatManager manager = new();

		bool result = await manager.CheckWinCondition();

		Assert.False(result);
		Assert.True(manager.RawInProgress);
		Assert.Equal(0, manager.OriginalCalls);
		Assert.Equal(1, FakeCompletionCoordinator.NullResultFallbacks);
		Assert.Equal(1, FakeCompletionCoordinator.SkippedObservations);
	}

	private static async Task OriginalFalseCoordinatesCompletion()
	{
		ResetCompletionScenario();
		FakeCombatManager manager = new()
		{
			CanComplete = true
		};

		bool result = await manager.CheckWinCondition();

		Assert.True(result);
		Assert.False(manager.RawInProgress);
		Assert.Equal(1, manager.OriginalCalls);
		Assert.Equal(1, manager.EndCalls);
		Assert.Equal(1, FakeCompletionCoordinator.RunOriginalObservations);
		Assert.Equal(0, FakeCompletionCoordinator.SkippedObservations);
	}

	private static async Task PostfixPseudoSuccessCoordinatesCompletion()
	{
		ResetCompletionScenario();
		ForcedCheckResultPostfix.Enabled = true;
		FakeCombatManager manager = new()
		{
			CanComplete = true
		};

		bool result = await manager.CheckWinCondition();

		Assert.True(result);
		Assert.False(manager.RawInProgress);
		Assert.Equal(1, manager.OriginalCalls);
		Assert.Equal(1, manager.EndCalls);
		Assert.Equal(1, ForcedCheckResultPostfix.Replacements);
		Assert.Equal(1, FakeCompletionCoordinator.RunOriginalObservations);
	}

	private static async Task InterceptedAddDoesNotAwaitStabilization()
	{
		ResetAddScenario();
		FakeCreature creature = new()
		{
			Erased = true
		};
		FakeAddProducer producer = new();

		Task production = producer.Produce(creature, count: 3);
		await production.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.Equal(3, producer.Produced);
		Assert.Equal(0, producer.OriginalAddCalls);
		Assert.Equal(3, FakeAddStabilizer.Scheduled);
		Assert.Equal(0, FakeAddStabilizer.Completed);
		FakeAddStabilizer.Release();
	}

	private static async Task SkippedAddWithoutTaskUsesCompletedFallback()
	{
		ResetAddScenario(HighPriorityAddSkipMode.WithoutResult);
		FakeCreature creature = new()
		{
			Erased = true
		};
		FakeAddProducer producer = new();

		Task result = producer.Add(creature);

		Assert.True(result.IsCompletedSuccessfully);
		await result;
		Assert.Equal(0, producer.OriginalAddCalls);
		Assert.Equal(1, ErasureAsyncAddGuard.NullResultFallbacks);
	}

	private static async Task ConcurrentChecksInvokeEndOnce()
	{
		ResetCompletionScenario();
		TaskCompletionSource<bool> endStarted =
			new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<bool> releaseEnd =
			new(TaskCreationOptions.RunContinuationsAsynchronously);
		FakeCombatManager manager = new()
		{
			CanComplete = true,
			EndStarted = endStarted,
			ReleaseEnd = releaseEnd
		};

		Task<bool> first = manager.CheckWinCondition();
		await endStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		Task<bool> second = manager.CheckWinCondition();
		await Task.Yield();

		Assert.Equal(1, manager.EndCalls);
		releaseEnd.SetResult(true);
		bool[] results = await Task.WhenAll(first, second);

		Assert.True(results[0]);
		Assert.True(results[1]);
		Assert.Equal(1, manager.EndCalls);
	}

	private static async Task ReentrantEndHookDoesNotAwaitItself()
	{
		ResetCompletionScenario();
		FakeCombatManager manager = new()
		{
			CanComplete = true
		};
		bool? nestedResult = null;
		manager.EndHook = async () =>
		{
			nestedResult = await manager.CheckWinCondition();
		};

		bool result = await manager.CheckWinCondition()
			.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.True(result);
		Assert.False(nestedResult
			?? throw new InvalidOperationException(
				"Expected the reentrant check to finish."));
		Assert.Equal(1, manager.EndCalls);
		Assert.False(manager.RawInProgress);
	}

	private static async Task SkippedEndBodyCanRetry()
	{
		ResetCompletionScenario();
		HighPriorityEndGuard.SkipNextBody();
		FakeCombatManager manager = new()
		{
			CanComplete = true
		};

		bool first = await manager.CheckWinCondition();
		bool second = await manager.CheckWinCondition();

		Assert.False(first);
		Assert.True(second);
		Assert.False(manager.RawInProgress);
		Assert.Equal(1, manager.EndCalls);
		Assert.Equal(2, FakeCompletionCoordinator.EndAttempts);
		Assert.Equal(1, FakeCompletionCoordinator.SkippedEndBodies);
	}

	private static async Task IncompleteEndAttemptIsNotRepeated()
	{
		ResetCompletionScenario();
		FakeCombatManager manager = new()
		{
			CanComplete = true,
			CompleteOnEnd = false
		};

		bool first = await manager.CheckWinCondition();
		bool second = await manager.CheckWinCondition();

		Assert.False(first);
		Assert.False(second);
		Assert.True(manager.RawInProgress);
		Assert.Equal(2, manager.OriginalCalls);
		Assert.Equal(1, manager.EndCalls);
		Assert.Equal(1, FakeCompletionCoordinator.EndAttempts);
		Assert.Equal(1, FakeCompletionCoordinator.IndeterminateEnds);
	}

	private static async Task OriginalExceptionIsNotSwallowed()
	{
		ResetCompletionScenario();
		TestException expected = new("original failure");
		FakeCombatManager manager = new()
		{
			OriginalException = expected
		};

		Exception actual = await Assert.ThrowsAsync(
			async () => await manager.CheckWinCondition());

		Assert.Same(expected, actual);
		Assert.Equal(1, FakeCompletionCoordinator.FinalizerCalls);
		Assert.Equal(0, FakeCompletionCoordinator.ResultWrappers);
	}

	private static void ResetCompletionScenario(
		HighPrioritySkipMode skipMode = HighPrioritySkipMode.Disabled)
	{
		HighPriorityCheckGuard.Mode = skipMode;
		ForcedCheckResultPostfix.Reset();
		HighPriorityEndGuard.Reset();
		FakeCompletionCoordinator.ResetObservations();
	}

	private static void ResetAddScenario(
		HighPriorityAddSkipMode skipMode =
			HighPriorityAddSkipMode.Disabled)
	{
		HighPriorityAddGuard.Mode = skipMode;
		ErasureAsyncAddGuard.Reset();
		FakeAddStabilizer.Reset();
	}

	private sealed class FakeCreature
	{
		public bool Erased;
		public bool Attached = true;
		public bool NodePresent = true;
		public bool MoveActive = true;
		public int Hp = 100;
		public List<string> Powers = ["guard"];

		public void SetHp(int value)
		{
			Hp = value;
		}

		public void Remove()
		{
			Attached = false;
			NodePresent = false;
		}

		public void Kill()
		{
			SetHp(0);
			Remove();
		}

		public void Add()
		{
			Attached = true;
			NodePresent = true;
			MoveActive = true;
			Hp = 100;
		}
	}

	private sealed class FakeCombatManager
	{
		private int _endCalls;
		private int _originalCalls;

		public bool RawInProgress = true;
		public bool CanComplete;
		public bool CompleteOnEnd = true;
		public Exception? OriginalException;
		public TaskCompletionSource<bool>? EndStarted;
		public TaskCompletionSource<bool>? ReleaseEnd;
		public Func<Task>? EndHook;

		public int EndCalls => Volatile.Read(ref _endCalls);

		public int OriginalCalls => Volatile.Read(ref _originalCalls);

		public Task<bool> CheckWinCondition()
		{
			Interlocked.Increment(ref _originalCalls);
			if (OriginalException is not null)
			{
				throw OriginalException;
			}

			return Task.FromResult(false);
		}

		public async Task EndCombatInternal()
		{
			Interlocked.Increment(ref _endCalls);
			EndStarted?.TrySetResult(true);
			if (EndHook is not null)
			{
				await EndHook();
			}
			if (ReleaseEnd is not null)
			{
				await ReleaseEnd.Task;
			}
			if (CompleteOnEnd)
			{
				RawInProgress = false;
			}
		}
	}

	private sealed class FakeAddProducer
	{
		private int _originalAddCalls;
		private int _produced;

		public int OriginalAddCalls =>
			Volatile.Read(ref _originalAddCalls);

		public int Produced => Volatile.Read(ref _produced);

		public Task Add(FakeCreature creature)
		{
			Interlocked.Increment(ref _originalAddCalls);
			return Task.CompletedTask;
		}

		public async Task Produce(FakeCreature creature, int count)
		{
			for (int index = 0; index < count; index++)
			{
				await Add(creature);
				Interlocked.Increment(ref _produced);
			}
		}
	}

	private static class FakeAddStabilizer
	{
		private static StabilizationState _state = new();

		public static int Scheduled =>
			Volatile.Read(ref _state).Scheduled;

		public static int Completed =>
			Volatile.Read(ref _state).Completed;

		public static void Reset()
		{
			Volatile.Write(ref _state, new StabilizationState());
		}

		public static void Schedule()
		{
			StabilizationState state = Volatile.Read(ref _state);
			Interlocked.Increment(ref state.Scheduled);
			_ = CompleteAfterRelease(state);
		}

		public static void Release()
		{
			Volatile.Read(ref _state).Release.TrySetResult(true);
		}

		private static async Task CompleteAfterRelease(
			StabilizationState state)
		{
			await state.Release.Task;
			Interlocked.Increment(ref state.Completed);
		}

		private sealed class StabilizationState
		{
			public int Scheduled;

			public int Completed;

			public TaskCompletionSource<bool> Release { get; } =
				new(TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}

	private enum HighPriorityAddSkipMode
	{
		Disabled,
		WithoutResult
	}

	[HarmonyPatch(
		typeof(FakeAddProducer),
		nameof(FakeAddProducer.Add))]
	private static class HighPriorityAddGuard
	{
		public static HighPriorityAddSkipMode Mode;

		[HarmonyPrefix]
		[HarmonyPriority(EarlierIndependentPatchPriority)]
		private static bool Prefix(ref Task? __result)
		{
			if (Mode == HighPriorityAddSkipMode.Disabled)
			{
				return true;
			}

			return false;
		}
	}

	[HarmonyPatch(
		typeof(FakeAddProducer),
		nameof(FakeAddProducer.Add))]
	private static class ErasureAsyncAddGuard
	{
		private static int _nullResultFallbacks;

		public static int NullResultFallbacks =>
			Volatile.Read(ref _nullResultFallbacks);

		public static void Reset()
		{
			Volatile.Write(ref _nullResultFallbacks, 0);
		}

		[HarmonyPrefix]
		[HarmonyPriority(SubjectPatchPriority)]
		private static bool Prefix(
			FakeCreature creature,
			ref Task? __result)
		{
			if (!creature.Erased)
			{
				return true;
			}

			FakeAddStabilizer.Schedule();
			__result = Task.CompletedTask;
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(
			FakeCreature creature,
			ref Task? __result)
		{
			if (!creature.Erased || __result is not null)
			{
				return;
			}

			Interlocked.Increment(ref _nullResultFallbacks);
			__result = Task.CompletedTask;
		}
	}

	private enum HighPrioritySkipMode
	{
		Disabled,
		AssignFalseResult,
		WithoutResult
	}

	[HarmonyPatch(
		typeof(FakeCombatManager),
		nameof(FakeCombatManager.EndCombatInternal))]
	private static class HighPriorityEndGuard
	{
		private static int _bodiesToSkip;

		public static void SkipNextBody()
		{
			Volatile.Write(ref _bodiesToSkip, 1);
		}

		public static void Reset()
		{
			Volatile.Write(ref _bodiesToSkip, 0);
		}

		[HarmonyPrefix]
		[HarmonyPriority(EarlierIndependentPatchPriority)]
		private static bool Prefix(ref Task? __result)
		{
			if (Interlocked.Exchange(ref _bodiesToSkip, 0) == 0)
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}

	[HarmonyPatch(
		typeof(FakeCombatManager),
		nameof(FakeCombatManager.CheckWinCondition))]
	private static class HighPriorityCheckGuard
	{
		public static HighPrioritySkipMode Mode;

		[HarmonyPrefix]
		[HarmonyPriority(EarlierIndependentPatchPriority)]
		private static bool Prefix(ref Task<bool>? __result)
		{
			if (Mode == HighPrioritySkipMode.Disabled)
			{
				return true;
			}

			if (Mode == HighPrioritySkipMode.AssignFalseResult)
			{
				__result = Task.FromResult(false);
			}
			return false;
		}
	}

	[HarmonyPatch(
		typeof(FakeCombatManager),
		nameof(FakeCombatManager.CheckWinCondition))]
	private static class ForcedCheckResultPostfix
	{
		private static int _replacements;

		public static bool Enabled;

		public static int Replacements =>
			Volatile.Read(ref _replacements);

		public static void Reset()
		{
			Enabled = false;
			Volatile.Write(ref _replacements, 0);
		}

		[HarmonyPostfix]
		private static void Postfix(ref Task<bool>? __result)
		{
			if (!Enabled)
			{
				return;
			}

			Interlocked.Increment(ref _replacements);
			__result = Task.FromResult(true);
		}
	}

	[HarmonyPatch(
		typeof(FakeCombatManager),
		nameof(FakeCombatManager.CheckWinCondition))]
	private static class CompletionResultGuard
	{
		[HarmonyPrefix]
		[HarmonyPriority(SubjectPatchPriority)]
		private static void Prefix(
			FakeCombatManager __instance,
			out CompletionCapture __state)
		{
			__state = new CompletionCapture(__instance);
			FakeCompletionCoordinator.RecordCapturePrefix();
		}

		[HarmonyFinalizer]
		[HarmonyPriority(Priority.Last)]
		private static Exception? Finalizer(
			FakeCombatManager __instance,
			CompletionCapture? __state,
			ref Task<bool>? __result,
			Exception? __exception,
			bool __runOriginal)
		{
			return FakeCompletionCoordinator.FinalizeResult(
				__instance,
				__state,
				ref __result,
				__exception,
				__runOriginal);
		}
	}

	private sealed record CompletionCapture(FakeCombatManager Manager);

	private static class FakeCompletionCoordinator
	{
		private static readonly ConditionalWeakTable<
			FakeCombatManager,
			CompletionState> States = new();

		private static readonly AsyncLocal<FakeCombatManager?> ActiveOwner =
			new();

		private static int _finalizerCalls;
		private static int _nullResultFallbacks;
		private static int _resultWrappers;
		private static int _runOriginalObservations;
		private static int _skippedObservations;
		private static int _capturePrefixCalls;
		private static int _nonNullCaptureStates;
		private static int _endAttempts;
		private static int _skippedEndBodies;
		private static int _indeterminateEnds;
		private static FakeCombatManager? _lastCapturedManager;

		public static int FinalizerCalls =>
			Volatile.Read(ref _finalizerCalls);

		public static int NullResultFallbacks =>
			Volatile.Read(ref _nullResultFallbacks);

		public static int ResultWrappers =>
			Volatile.Read(ref _resultWrappers);

		public static int RunOriginalObservations =>
			Volatile.Read(ref _runOriginalObservations);

		public static int SkippedObservations =>
			Volatile.Read(ref _skippedObservations);

		public static int CapturePrefixCalls =>
			Volatile.Read(ref _capturePrefixCalls);

		public static int NonNullCaptureStates =>
			Volatile.Read(ref _nonNullCaptureStates);

		public static int EndAttempts =>
			Volatile.Read(ref _endAttempts);

		public static int SkippedEndBodies =>
			Volatile.Read(ref _skippedEndBodies);

		public static int IndeterminateEnds =>
			Volatile.Read(ref _indeterminateEnds);

		public static FakeCombatManager? LastCapturedManager =>
			Volatile.Read(ref _lastCapturedManager);

		public static void RecordCapturePrefix()
		{
			Interlocked.Increment(ref _capturePrefixCalls);
		}

		public static void ResetObservations()
		{
			Volatile.Write(ref _finalizerCalls, 0);
			Volatile.Write(ref _nullResultFallbacks, 0);
			Volatile.Write(ref _resultWrappers, 0);
			Volatile.Write(ref _runOriginalObservations, 0);
			Volatile.Write(ref _skippedObservations, 0);
			Volatile.Write(ref _capturePrefixCalls, 0);
			Volatile.Write(ref _nonNullCaptureStates, 0);
			Volatile.Write(ref _endAttempts, 0);
			Volatile.Write(ref _skippedEndBodies, 0);
			Volatile.Write(ref _indeterminateEnds, 0);
			Volatile.Write(ref _lastCapturedManager, null);
		}

		public static Exception? FinalizeResult(
			FakeCombatManager manager,
			CompletionCapture? capture,
			ref Task<bool>? result,
			Exception? exception,
			bool runOriginal)
		{
			Interlocked.Increment(ref _finalizerCalls);
			if (capture is not null)
			{
				Interlocked.Increment(ref _nonNullCaptureStates);
				Volatile.Write(
					ref _lastCapturedManager,
					capture.Manager);
			}
			if (runOriginal)
			{
				Interlocked.Increment(ref _runOriginalObservations);
			}
			else
			{
				Interlocked.Increment(ref _skippedObservations);
			}

			if (exception is not null)
			{
				return exception;
			}

			if (result is null)
			{
				Interlocked.Increment(ref _nullResultFallbacks);
				result = Task.FromResult(false);
			}

			Interlocked.Increment(ref _resultWrappers);
			result = ObserveResult(manager, result);
			return null;
		}

		private static async Task<bool> ObserveResult(
			FakeCombatManager manager,
			Task<bool> originalResult)
		{
			await originalResult;
			if (!manager.RawInProgress)
			{
				return true;
			}
			if (!manager.CanComplete)
			{
				return false;
			}

			return await CompleteOnce(manager);
		}

		private static async Task<bool> CompleteOnce(
			FakeCombatManager manager)
		{
			CompletionState state = States.GetValue(
				manager,
				static _ => new CompletionState());
			Task<bool> attempt;
			TaskCompletionSource<bool>? owner = null;
			lock (state.Gate)
			{
				if (!manager.RawInProgress)
				{
					return true;
				}

				if (state.Disposition
					== FakeCompletionDisposition.Indeterminate
					|| state.Disposition
						== FakeCompletionDisposition.Completed)
				{
					return false;
				}

				if (state.Disposition
					== FakeCompletionDisposition.Running)
				{
					if (ReferenceEquals(ActiveOwner.Value, manager))
					{
						return false;
					}

					attempt = state.Attempt
						?? Task.FromResult(false);
				}
				else
				{
					owner = new TaskCompletionSource<bool>(
						TaskCreationOptions.RunContinuationsAsynchronously);
					attempt = owner.Task;
					state.Attempt = attempt;
					state.Disposition =
						FakeCompletionDisposition.Running;
				}
			}

			if (owner is null)
			{
				return await attempt;
			}

			return await RunOwnedAttempt(manager, state, owner);
		}

		private static async Task<bool> RunOwnedAttempt(
			FakeCombatManager manager,
			CompletionState state,
			TaskCompletionSource<bool> completion)
		{
			FakeCombatManager? previousOwner = ActiveOwner.Value;
			ActiveOwner.Value = manager;
			int endCallsBefore = manager.EndCalls;
			Interlocked.Increment(ref _endAttempts);
			try
			{
				await manager.EndCombatInternal();
				bool bodyRan = manager.EndCalls > endCallsBefore;
				bool completed = !manager.RawInProgress;
				FinishEndAttempt(
					state,
					completion.Task,
					completed
						? FakeCompletionDisposition.Completed
						: bodyRan
							? FakeCompletionDisposition.Indeterminate
							: FakeCompletionDisposition.Idle);
				if (!bodyRan)
				{
					Interlocked.Increment(ref _skippedEndBodies);
				}
				else if (!completed)
				{
					Interlocked.Increment(ref _indeterminateEnds);
				}
				completion.TrySetResult(completed);
				return completed;
			}
			catch (Exception exception)
			{
				bool bodyRan = manager.EndCalls > endCallsBefore;
				FinishEndAttempt(
					state,
					completion.Task,
					bodyRan
						? FakeCompletionDisposition.Indeterminate
						: FakeCompletionDisposition.Idle);
				completion.TrySetException(exception);
				throw;
			}
			finally
			{
				ActiveOwner.Value = previousOwner;
			}
		}

		private static void FinishEndAttempt(
			CompletionState state,
			Task<bool> attempt,
			FakeCompletionDisposition disposition)
		{
			lock (state.Gate)
			{
				if (!ReferenceEquals(state.Attempt, attempt))
				{
					return;
				}

				state.Disposition = disposition;
				if (disposition == FakeCompletionDisposition.Idle)
				{
					state.Attempt = null;
				}
			}
		}

		private enum FakeCompletionDisposition
		{
			Idle,
			Running,
			Completed,
			Indeterminate
		}

		private sealed class CompletionState
		{
			public object Gate { get; } = new();

			public FakeCompletionDisposition Disposition { get; set; }

			public Task<bool>? Attempt { get; set; }
		}
	}

	private static class RawConverger
	{
		public static void Converge(FakeCreature creature)
		{
			creature.Powers.Clear();
			creature.Hp = 0;
			creature.MoveActive = false;
			creature.NodePresent = false;
			creature.Attached = false;
		}
	}

	private static class GuardCounters
	{
		public static int HpSetter;
		public static int Remove;
		public static int Kill;
		public static int AddPrefix;
		public static int AddPostfix;
	}

	[HarmonyPatch(typeof(FakeCreature), nameof(FakeCreature.SetHp))]
	private static class HpSetterGuard
	{
		[HarmonyPrefix]
		private static bool Prefix(ref int value)
		{
			GuardCounters.HpSetter++;
			if (value <= 0)
			{
				value = 1;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(FakeCreature), nameof(FakeCreature.Remove))]
	private static class RemovalGuard
	{
		[HarmonyPrefix]
		private static bool Prefix()
		{
			GuardCounters.Remove++;
			return false;
		}
	}

	[HarmonyPatch(typeof(FakeCreature), nameof(FakeCreature.Kill))]
	private static class KillGuard
	{
		[HarmonyPrefix]
		private static bool Prefix()
		{
			GuardCounters.Kill++;
			return false;
		}

		[HarmonyPostfix]
		private static void Postfix(FakeCreature __instance)
		{
			__instance.Hp = 1;
		}

		[HarmonyFinalizer]
		private static Exception? Finalizer()
		{
			return null;
		}
	}

	[HarmonyPatch(typeof(FakeCreature), nameof(FakeCreature.Add))]
	private static class CommonAddGuard
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Normal)]
		private static bool Prefix()
		{
			GuardCounters.AddPrefix++;
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPriority(Priority.Normal)]
		private static void Postfix(FakeCreature __instance)
		{
			GuardCounters.AddPostfix++;
			__instance.Attached = true;
			__instance.NodePresent = true;
			__instance.MoveActive = true;
			__instance.Hp = 1;
		}

		[HarmonyFinalizer]
		private static Exception? Finalizer()
		{
			return null;
		}
	}

	[HarmonyPatch(typeof(FakeCreature), nameof(FakeCreature.Add))]
	private static class ErasureAddGuard
	{
		[HarmonyPrefix]
		[HarmonyPriority(SubjectPatchPriority)]
		private static bool Prefix(FakeCreature __instance)
		{
			return !__instance.Erased;
		}

		[HarmonyPostfix]
		[HarmonyPriority(SubjectPatchPriority)]
		private static void Postfix(FakeCreature __instance)
		{
			if (__instance.Erased)
			{
				RawConverger.Converge(__instance);
			}
		}

		[HarmonyFinalizer]
		[HarmonyPriority(SubjectPatchPriority)]
		private static Exception? Finalizer(
			FakeCreature __instance,
			Exception? __exception)
		{
			if (__instance.Erased)
			{
				RawConverger.Converge(__instance);
				return null;
			}
			return __exception;
		}
	}

	private sealed class TestException(string message) : Exception(message);

	private static class Assert
	{
		public static void True(bool condition)
		{
			if (!condition)
			{
				throw new InvalidOperationException(
					"Expected condition to be true.");
			}
		}

		public static void False(bool condition)
		{
			if (condition)
			{
				throw new InvalidOperationException(
					"Expected condition to be false.");
			}
		}

		public static void Equal<T>(T expected, T actual)
			where T : notnull
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
			{
				throw new InvalidOperationException(
					$"Expected {expected}, got {actual}.");
			}
		}

		public static void Same(object expected, object actual)
		{
			if (!ReferenceEquals(expected, actual))
			{
				throw new InvalidOperationException(
					"Expected both references to be identical.");
			}
		}

		public static async Task<Exception> ThrowsAsync(
			Func<Task> action)
		{
			try
			{
				await action();
			}
			catch (Exception exception)
			{
				return exception;
			}

			throw new InvalidOperationException(
				"Expected the action to throw.");
		}
	}
}
