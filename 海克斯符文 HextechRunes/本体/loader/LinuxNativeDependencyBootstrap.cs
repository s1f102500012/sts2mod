using System.Runtime.InteropServices;
using MegaCrit.Sts2.Core.Logging;

namespace HextechRunes.Loader;

internal static class LinuxNativeDependencyBootstrap
{
	private const int RtldNow = 0x2;
	private const int RtldNoLoad = 0x4;
	private const int RtldGlobal = 0x100;
	private const string LibgccName = "libgcc_s.so.1";

	private static readonly object Gate = new();
	private static bool _attempted;

	// Harmony's generated exception helper resolves unwind symbols after startup,
	// so this handle must remain alive and globally visible for the process lifetime.
	private static IntPtr _libgccHandle;

	internal static void EnsureHarmonyRuntimeDependenciesVisible()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return;
		}

		lock (Gate)
		{
			if (_attempted)
			{
				return;
			}

			_attempted = true;
			try
			{
				_libgccHandle = Dlopen(LibgccName, RtldNow | RtldNoLoad | RtldGlobal);
				if (_libgccHandle == IntPtr.Zero)
				{
					_libgccHandle = Dlopen(LibgccName, RtldNow | RtldGlobal);
				}

				if (_libgccHandle == IntPtr.Zero)
				{
					Log.Warn(
						"[HextechRunes.Loader] Could not expose libgcc_s.so.1 globally; " +
						"Harmony patches may fail to initialize on native Linux.");
					return;
				}

				Log.Info(
					"[HextechRunes.Loader] Exposed libgcc_s.so.1 to Harmony's native helper.");
			}
			catch (Exception exception)
			{
				Log.Warn(
					$"[HextechRunes.Loader] Failed to expose libgcc_s.so.1 globally: {exception.Message}");
			}
		}
	}

	private static IntPtr Dlopen(string fileName, int flags)
	{
		try
		{
			return DlopenGlibc(fileName, flags);
		}
		catch (DllNotFoundException)
		{
			return DlopenGeneric(fileName, flags);
		}
	}

	[DllImport("libdl.so.2", EntryPoint = "dlopen")]
	private static extern IntPtr DlopenGlibc(string fileName, int flags);

	[DllImport("libdl.so", EntryPoint = "dlopen")]
	private static extern IntPtr DlopenGeneric(string fileName, int flags);
}
