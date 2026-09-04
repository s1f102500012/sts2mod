using System;
using System.Threading;

namespace AITeammate.Scripts;

internal static class AiTeammateSimulationRuntime
{
    private static readonly AsyncLocal<int> PresentationSuppressionDepth = new();

    public static bool IsPresentationSuppressed => PresentationSuppressionDepth.Value > 0;

    public static IDisposable SuppressPresentation()
    {
        PresentationSuppressionDepth.Value++;
        return new PresentationSuppressionScope();
    }

    private sealed class PresentationSuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            PresentationSuppressionDepth.Value = Math.Max(0, PresentationSuppressionDepth.Value - 1);
            _disposed = true;
        }
    }
}
