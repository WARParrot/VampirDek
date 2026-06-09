using UnityEngine;

namespace Combat.UI
{
    /// <summary>
    /// Workaround for the known Unity UGUI 2022.3.x bug
    /// (com.unity.ugui@d8a2716f3013):
    ///
    ///   IndexOutOfRangeException at UnityEngine.UI.Collections.IndexedSet`1[T].get_Item
    ///   ... CanvasUpdateRegistry.PerformUpdate ()
    ///
    /// Root cause is inside Unity itself: during PerformUpdate(), iterating the global
    /// m_LayoutRebuildQueue while a Rebuild() callback synchronously removes another
    /// entry from the same IndexedSet (via UnRegisterCanvasElementForRebuild) — the
    /// swap-and-truncate inside IndexedSet shifts indices and the for-loop OOBs.
    ///
    /// The exception is harmless — the rebuild finishes for the surviving entries and
    /// rendering continues — but it spams the console. Unity's fix for this is in 2023+.
    ///
    /// We can't patch Unity's loop, so we install an ILogHandler that filters ONLY this
    /// specific exception/stack signature and forwards everything else untouched.
    /// </summary>
    internal static class UguiIndexedSetExceptionFilter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (Debug.unityLogger.logHandler is FilteringHandler) return;
            Debug.unityLogger.logHandler = new FilteringHandler(Debug.unityLogger.logHandler);
        }

        private sealed class FilteringHandler : ILogHandler
        {
            private readonly ILogHandler _inner;
            public FilteringHandler(ILogHandler inner) { _inner = inner; }

            public void LogException(System.Exception exception, Object context)
            {
                if (IsKnownUguiIndexedSetBug(exception))
                    return; // swallow — harmless, well-documented engine bug
                _inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                _inner.LogFormat(logType, context, format, args);
            }

            private static bool IsKnownUguiIndexedSetBug(System.Exception ex)
            {
                if (ex is not System.IndexOutOfRangeException) return false;
                var st = ex.StackTrace;
                if (string.IsNullOrEmpty(st)) return false;
                // Match the exact engine frames so we don't hide our own bugs.
                return st.Contains("UnityEngine.UI.Collections.IndexedSet")
                    && st.Contains("CanvasUpdateRegistry.PerformUpdate");
            }
        }
    }
}
