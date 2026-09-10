using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

/// <summary>
/// Installs the official login-bonus DLC bundled in the Korean Android APK.
/// Existing player saves and login-bonus progress are never modified.
/// </summary>
public static class UMOOfficialLoginBonusInstaller
{
    public const string PackageName = "offcial-login-bonuses";
    public const int PackageVersion = 1;
    public const string ResourcePath = "EmbeddedDlc/offcial-login-bonuses_1_Android";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBeforeGameBoot()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            InstallEmbeddedPackage();
        }
        catch(Exception e)
        {
            // The game remains usable without this optional content. Keep the
            // error in the log so a damaged APK can be diagnosed.
            Debug.LogError("Official login bonus DLC auto-install failed: " + e);
        }
#endif
    }

    private static void InstallEmbeddedPackage()
    {
        string root = DlcManager.DlcPath;
        string enabled = Path.Combine(root, PackageName);
        string disabled = Path.Combine(root, "_" + PackageName);

        if(IsCurrentPackage(enabled))
            return;
        if(IsCurrentPackage(disabled))
        {
            Directory.CreateDirectory(root);
            if(Directory.Exists(enabled))
                Directory.Delete(enabled, true);
            Directory.Move(disabled, enabled);
            Debug.Log("Official login bonus DLC activated.");
            return;
        }

        TextAsset archive = Resources.Load<TextAsset>(ResourcePath);
        if(archive == null || archive.bytes == null || archive.bytes.Length == 0)
            throw new FileNotFoundException("Embedded official login bonus archive is missing.");

        Directory.CreateDirectory(root);
        string temporary = Path.Combine(root, "." + PackageName + "-install");
        string zipPath = Path.Combine(root, "." + PackageName + ".zip");
        string backup = Path.Combine(root, "." + PackageName + "-backup");
        if(Directory.Exists(temporary)) Directory.Delete(temporary, true);
        if(Directory.Exists(backup)) Directory.Delete(backup, true);
        File.WriteAllBytes(zipPath, archive.bytes);
        try
        {
            new FastZip().ExtractZip(zipPath, temporary, null);
            if(!IsCurrentPackage(temporary))
                throw new InvalidDataException("Embedded official login bonus package is invalid.");

            if(Directory.Exists(enabled)) Directory.Move(enabled, backup);
            if(Directory.Exists(disabled)) Directory.Delete(disabled, true);
            Directory.Move(temporary, enabled);
            if(Directory.Exists(backup)) Directory.Delete(backup, true);
            Debug.Log("Official login bonus DLC installed and activated.");
        }
        catch
        {
            if(!Directory.Exists(enabled) && Directory.Exists(backup))
                Directory.Move(backup, enabled);
            throw;
        }
        finally
        {
            if(File.Exists(zipPath)) File.Delete(zipPath);
            if(Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private static bool IsCurrentPackage(string path)
    {
        string info = Path.Combine(path, "dlc.json");
        if(!File.Exists(info)) return false;
        string json = File.ReadAllText(info);
        return json.IndexOf("\"package_name\":\"" + PackageName + "\"", StringComparison.Ordinal) >= 0 &&
            json.IndexOf("\"version\":" + PackageVersion, StringComparison.Ordinal) >= 0;
    }
}
