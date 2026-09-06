using System;
using System.IO;
using UnityEngine;

// Keeps the Windows build portable without changing Android or Editor storage.
// Game assets remain in the executable-side Data directory; this root is only
// for profiles, save files and other small pieces of player state.
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
        string legacyRoot = Application.persistentDataPath;
        string portableRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PortableDirectoryName));
        try
        {
            Directory.CreateDirectory(portableRoot);
            MigrateLegacySave(legacyRoot, portableRoot);
            return portableRoot;
        }
        catch(Exception e)
        {
            Debug.LogWarning("PC portable save folder is unavailable; using LocalLow instead. " + e.Message);
            return legacyRoot;
        }
    }

    private static void MigrateLegacySave(string legacyRoot, string portableRoot)
    {
        if(string.Equals(legacyRoot, portableRoot, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(legacyRoot))
            return;

        string[] directories = { "Profiles", "SaveData", "40", "50", "60", "61" };
        foreach(string directory in directories)
        {
            string source = Path.Combine(legacyRoot, directory);
            if(Directory.Exists(source))
                CopyDirectoryMissingFiles(source, Path.Combine(portableRoot, directory));
        }

        string[] files = { "pref.json", "fca", "Local1.txt", "Local2.txt" };
        foreach(string file in files)
        {
            string source = Path.Combine(legacyRoot, file);
            string destination = Path.Combine(portableRoot, file);
            if(File.Exists(source) && !File.Exists(destination))
                File.Copy(source, destination, false);
        }
    }

    private static void CopyDirectoryMissingFiles(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(string file in Directory.GetFiles(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            if(!File.Exists(target))
                File.Copy(file, target, false);
        }
        foreach(string directory in Directory.GetDirectories(source))
            CopyDirectoryMissingFiles(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
#endif
}
