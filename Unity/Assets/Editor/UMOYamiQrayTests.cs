#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

public static class UMOYamiQrayTests
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/yami-dlc-test-" + Guid.NewGuid().ToString("N")));
        try
        {
            UMOYamiQrayInstaller.InstallAt(root);
            foreach(string member in UMOYamiQrayInstaller.Members)
            {
                string package = "yamiQray_" + member;
                Assert(File.Exists(Path.Combine(root, "_" + package, "dlc.json")), "default OFF " + package);
                Assert(!Directory.Exists(Path.Combine(root, package)), "not auto-enabled " + package);
            }
            Directory.Move(Path.Combine(root, "_yamiQray_freyja"), Path.Combine(root, "yamiQray_freyja"));
            string sentinel = Path.Combine(root, "yamiQray_freyja", "keep.txt");
            File.WriteAllText(sentinel, "user content");
            UMOYamiQrayInstaller.InstallAt(root);
            Assert(File.ReadAllText(sentinel) == "user content", "preserve installed content");
            Assert(!Directory.Exists(Path.Combine(root, "_yamiQray_freyja")), "preserve ON");
            Assert(Directory.Exists(Path.Combine(root, "_yamiQray_reina")), "preserve OFF");
            Debug.Log("UMO YamiQray installation tests passed: five defaults OFF, repeat install, ON/OFF preserved.");
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void Assert(bool value, string name)
    {
        if(!value) throw new Exception("YamiQray test failed: " + name);
    }
}
#endif
