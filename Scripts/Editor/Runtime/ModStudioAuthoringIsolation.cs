namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class ModStudioAuthoringIsolation
{
    private static readonly object SyncRoot = new();
    private static int _projectModeDepth;

    public static bool IsProjectModeActive
    {
        get
        {
            lock (SyncRoot)
            {
                return _projectModeDepth > 0;
            }
        }
    }

    public static IDisposable EnterProjectMode()
    {
        lock (SyncRoot)
        {
            _projectModeDepth++;
        }

        return new Lease();
    }

    private static void ExitProjectMode()
    {
        lock (SyncRoot)
        {
            if (_projectModeDepth > 0)
            {
                _projectModeDepth--;
            }
        }
    }

    private sealed class Lease : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExitProjectMode();
        }
    }
}
