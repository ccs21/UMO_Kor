#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class UMODlcPathRegressionTests
{
    public static void Run()
    {
        Type proxy = typeof(DlcManager).Assembly.GetType("FileSystemProxy");
        Type settingsType = typeof(DlcManager).Assembly.GetType("RuntimeSettings");
        object settings = settingsType.GetProperty("CurrentSettings").GetValue(null, null);
        FieldInfo dataField = settingsType.GetField("DataDirectory");
        PropertyInfo languageField = settingsType.GetProperty("Language");
        object oldData = dataField.GetValue(settings);
        object oldLanguage = languageField.GetValue(settings);
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/dlc-path-test-" + Guid.NewGuid().ToString("N")));
        try
        {
            dataField.SetValue(settings, root);
            languageField.SetValue(settings, "ko");
            string dlcRoot = Path.Combine(root, "dlc");
            UMOYamiQrayInstaller.InstallAt(dlcRoot);
            int count = 0;
            foreach(string member in UMOYamiQrayInstaller.Members)
            {
                string package = "yamiQray_" + member;
                Directory.Move(Path.Combine(dlcRoot, "_" + package), Path.Combine(dlcRoot, package));
                string bundles = Path.Combine(dlcRoot, package, "bundles");
                foreach(string file in Directory.GetFiles(bundles, "*.xab", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(bundles.Length + 1).Replace('/', '\\');
                    // Exact Windows files.json registration pattern, including mixed separators.
                    string key = Path.Combine("/android/", relative);
                    string destination = "/" + Path.Combine("dlc", package, "bundles", relative);
                    proxy.GetMethod("AddDlcFile").Invoke(null, new object[] { key, destination });
                    string lookup = "/android/" + relative.Replace('\\', '/');
                    Assert((bool)proxy.GetMethod("DlcFileExists").Invoke(null, new object[] { lookup }), "DLC manifest lookup " + lookup);
                    string request = Application.persistentDataPath + "/data" + lookup;
                    string resolved = (string)proxy.GetMethod("ConvertPath").Invoke(null, new object[] { request });
                    Assert(File.Exists(resolved), "local DLC lookup " + lookup);
                    Assert(Path.GetFullPath(resolved) == Path.GetFullPath(file), "correct DLC target " + lookup);
                    string pcRequest = root.ToLowerInvariant() + lookup;
                    string pcResolved = (string)proxy.GetMethod("ConvertPath").Invoke(null, new object[] { pcRequest });
                    Assert(File.Exists(pcResolved) && Path.GetFullPath(pcResolved) == Path.GetFullPath(file), "PC absolute path " + lookup);
                    count++;
                }
            }
            Assert(count == 45, "all five DLC bundle sets");
            MethodInfo repair = settingsType.GetMethod("RepairLegacyLanguage");
            Assert((string)repair.Invoke(null, new object[] { "", false }) == "ko", "repair beta12 Japanese value");
            Assert((string)repair.Invoke(null, new object[] { "jp", false }) == "ko", "repair Japanese alias");
            Assert((string)repair.Invoke(null, new object[] { "", true }) == "", "preserve Japanese after repair");
            Assert((string)repair.Invoke(null, new object[] { "en", false }) == "en", "preserve other languages");
            GameObject popupObject = new GameObject("LanguageSaveRegression");
            try
            {
                UMOPopupLanguage popup = popupObject.AddComponent<UMOPopupLanguage>();
                popup.Save();
                Assert((string)languageField.GetValue(settings) == "ko", "unvisited language tab preserves Korean");
            }
            finally { UnityEngine.Object.DestroyImmediate(popupObject); }
            string title, body;
            Assert(EmbeddedKoreanLocalization.TryTranslateEarlyLiteral("StringLiteral_11929", out title) && title == "다운로드 에러", "early Korean title");
            Assert(EmbeddedKoreanLocalization.TryTranslateEarlyLiteral("StringLiteral_11930", out body) && body.Contains("파일 다운로드에 실패했습니다."), "early Korean body");
            languageField.SetValue(settings, "");
            Assert(!EmbeddedKoreanLocalization.TryTranslateEarlyLiteral("StringLiteral_11929", out title), "preserve Japanese language choice");
            proxy.GetMethod("ClearDlcFiles").Invoke(null, null);
            Assert(!(bool)proxy.GetMethod("DlcFileExists").Invoke(null, new object[] { "/android/ct/im/50400.xab" }), "clear stale DLC mappings");
            Debug.Log("UMO DLC path regression tests passed: 45 local bundles, early Korean errors, language choice, map reset.");
        }
        finally
        {
            proxy.GetMethod("ClearDlcFiles").Invoke(null, null);
            dataField.SetValue(settings, oldData);
            languageField.SetValue(settings, oldLanguage);
            if(Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void Assert(bool condition, string name)
    {
        if(!condition) throw new Exception("DLC regression failed: " + name);
    }
}
#endif
