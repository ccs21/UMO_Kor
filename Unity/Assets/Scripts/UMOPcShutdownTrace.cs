using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

// File-only tracing: finalizer threads must not call Unity's logging API.
public static class UMOPcShutdownTrace
{
    private static string path;
    private static readonly object gate = new object();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        path = Path.Combine(Application.persistentDataPath, "pc-shutdown-trace.log");
        Mark("START " + DateTime.UtcNow.ToString("O"));
        Application.quitting += () => Mark("APPLICATION QUITTING");
    }
#endif

    [Conditional("UNITY_STANDALONE_WIN")]
    public static void Mark(string stage)
    {
        if (path == null) return;
        try
        {
            lock (gate)
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " thread=" +
                    Thread.CurrentThread.ManagedThreadId + " " + stage + Environment.NewLine);
        }
        catch { /* A diagnostic must not prevent shutdown. */ }
    }
}
