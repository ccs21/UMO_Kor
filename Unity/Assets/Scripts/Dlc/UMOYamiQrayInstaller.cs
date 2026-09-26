using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

// Optional bundled costumes: install once, never change the player's ON/OFF choice.
public static class UMOYamiQrayInstaller
{
    public static readonly string[] Members = { "freyja", "mikumo", "kaname", "makina", "reina" };

    public static void Install()
    {
        InstallAt(DlcManager.DlcPath);
    }

    public static void InstallAt(string root)
    {
        foreach(string member in Members)
        {
            string package = "yamiQray_" + member;
            try
            {
                string enabled = Path.Combine(root, package);
                string disabled = Path.Combine(root, "_" + package);
                // Preserve existing installations, including manually installed newer versions.
                if(File.Exists(Path.Combine(enabled, "dlc.json")) ||
                    File.Exists(Path.Combine(disabled, "dlc.json"))) continue;
                TextAsset archive = Resources.Load<TextAsset>("BundledDlc/" + package + "_1_Android");
                if(archive == null) throw new FileNotFoundException("Missing bundled DLC: " + package);
                Directory.CreateDirectory(root);
                string staging = Path.Combine(root, "." + package + "-" + Guid.NewGuid().ToString("N"));
                string zip = staging + ".zip";
                try
                {
                    File.WriteAllBytes(zip, archive.bytes);
                    new FastZip().ExtractZip(zip, staging, null);
                    string info = File.ReadAllText(Path.Combine(staging, "dlc.json"));
                    if(!info.Contains("\"package_name\":\"" + package + "\""))
                        throw new InvalidDataException("Invalid bundled DLC: " + package);
                    // Do not overwrite an incomplete user directory automatically.
                    Directory.Move(staging, disabled);
                    Debug.Log("Installed optional DLC (OFF): " + package);
                }
                finally
                {
                    if(File.Exists(zip)) File.Delete(zip);
                    if(Directory.Exists(staging)) Directory.Delete(staging, true);
                    Resources.UnloadAsset(archive);
                }
            }
            catch(Exception e) { Debug.LogError("Optional DLC installation failed: " + package + ": " + e); }
        }
    }
}
