#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class UMOStandaloneWindowsWindow : MonoBehaviour
{
    private const string KoreanTitle = "\uC6B0\uD0C0\uB9C8\uD06C\uB85C\uC2A4";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(System.IntPtr window, string title);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, System.IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint processId);

    private delegate bool EnumWindowsCallback(System.IntPtr window, System.IntPtr parameter);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        GameObject helper = new GameObject("UMO Standalone Window");
        DontDestroyOnLoad(helper);
        helper.AddComponent<UMOStandaloneWindowsWindow>();
    }

    private IEnumerator Start()
    {
        // Unity can recreate or retitle its window while leaving the splash
        // screen and while applying the game's resolution. Reapply for the
        // first few seconds, always to this process's own main window.
        for(int frame = 0; frame < 600; frame++)
        {
            uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(delegate(System.IntPtr window, System.IntPtr parameter)
            {
                uint windowProcessId;
                GetWindowThreadProcessId(window, out windowProcessId);
                if(windowProcessId == currentProcessId)
                    SetWindowText(window, KoreanTitle);
                return true;
            }, System.IntPtr.Zero);
            yield return null;
        }
    }
}
#endif
