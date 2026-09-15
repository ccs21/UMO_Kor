using System.IO;
using UnityEngine;

// Windows builds only use the executable-side UserData directory. Never read
// or fall back to Unity's shared LocalLow directory: separate game folders must
// have separate saves, and deleting UserData must produce a clean reset.
public static class UMOPcSavePath
{
    private const string PortableDirectoryName = "UserData";
    private static readonly object Gate = new object();
    private static string root;

    public static string Root
    {
        get
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(string.IsNullOrEmpty(root))
            {
                lock(Gate)
                {
                    if(string.IsNullOrEmpty(root))
                        root = InitializePortableRoot();
                }
            }
            return root;
#else
            return Application.persistentDataPath;
#endif
        }
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static string InitializePortableRoot()
    {
        string portableRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PortableDirectoryName));
        Directory.CreateDirectory(portableRoot);
        return portableRoot;
    }
#endif
}
