#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class UMOKoreanWindowsBuild
{
    // Keep the legacy Windows product identifier so existing UMO profiles,
    // settings and save files stay under LocalLow/UtaMacross/UtaMacross.
    // UMOStandaloneWindowsWindow changes only the visible window title.
    private const string DefaultOutput = "Build/Windows/UMO_Kor/UMO_Kor.exe";
    private const string LegacyWindowsProductName = "UtaMacross";
    private const string WindowsIconAssetPath = "Assets/Editor/UMO_PC.png";
    private static readonly string[,] ExcludedTmpResources =
    {
        { "Assets/TextMesh Pro/Resources/TMP Settings.asset", "Assets/Editor/UMOStandaloneBuildExcluded/TMP Settings.asset" },
        { "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset", "Assets/Editor/UMOStandaloneBuildExcluded/EmojiOne.asset" },
        { "Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset", "Assets/Editor/UMOStandaloneBuildExcluded/Default Style Sheet.asset" },
    };

    public static void BuildDevelopment()
    {
        Build(BuildOptions.Development | BuildOptions.AllowDebugging);
    }

    public static void BuildRelease()
    {
        Build(BuildOptions.None);
    }

    // Unity 2018 caches the Resources build map early in editor startup. These
    // entry points let automated builds prepare it in a separate editor run.
    public static void PrepareResources()
    {
        MoveTmpResources(true);
    }

    public static void RestoreResources()
    {
        MoveTmpResources(false);
    }

    private static void Build(BuildOptions buildOptions)
    {
        if(!EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new Exception("Unable to switch the active build target to Windows 64-bit.");

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if(scenes.Length == 0)
            throw new Exception("No enabled scenes were found in EditorBuildSettings.");

        string output = GetCommandLineValue("-umoOutput") ?? DefaultOutput;
        if(!Path.IsPathRooted(output))
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        string originalProductName = PlayerSettings.productName;
        int originalWidth = PlayerSettings.defaultScreenWidth;
        int originalHeight = PlayerSettings.defaultScreenHeight;
        ResolutionDialogSetting originalResolutionDialog = PlayerSettings.displayResolutionDialog;
        bool originalResizableWindow = PlayerSettings.resizableWindow;
        Texture2D[] originalIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
        try
        {
            PlayerSettings.productName = LegacyWindowsProductName;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.Disabled;
            PlayerSettings.resizableWindow = true;
            Texture2D windowsIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(WindowsIconAssetPath);
            if(windowsIcon == null)
                throw new FileNotFoundException("Windows icon asset was not found", WindowsIconAssetPath);
            int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone,
                iconSizes.Select(size => windowsIcon).ToArray());
            Debug.Log("Windows icon slots: " + string.Join(",", iconSizes.Select(size => size.ToString()).ToArray()));
            MoveTmpResources(true);
            // Unity 2018 can log a preprocessing callback exception and still
            // emit a player. Validate required shader references synchronously.
            Resources.Load<ShaderList>("ShaderList").ReloadAssetList();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = buildOptions,
            });
            BuildSummary summary = report.summary;
            Debug.LogFormat("UMO Korean Windows build: result={0}, errors={1}, warnings={2}, bytes={3}, output={4}",
                summary.result, summary.totalErrors, summary.totalWarnings, summary.totalSize, output);
            if(summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + summary.result);

            CopyLibVlcRuntime(output);
            CopyInternationalCodePages(output);
            CopyDataManifests(output);
        }
        finally
        {
            MoveTmpResources(false);
            PlayerSettings.productName = originalProductName;
            PlayerSettings.defaultScreenWidth = originalWidth;
            PlayerSettings.defaultScreenHeight = originalHeight;
            PlayerSettings.displayResolutionDialog = originalResolutionDialog;
            PlayerSettings.resizableWindow = originalResizableWindow;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, originalIcons);
        }
    }

    private static void CopyDataManifests(string executablePath)
    {
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Data"));
        string destination = Path.Combine(Path.GetDirectoryName(executablePath), "Data");
        Directory.CreateDirectory(destination);

        foreach(string manifest in Directory.GetFiles(source, "Request*.json"))
        {
            string target = Path.Combine(destination, Path.GetFileName(manifest));
            // An archive-provided file list matches its payload and takes precedence.
            if(Path.GetFileName(manifest).Equals("RequestGetFiles.json", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                continue;
            File.Copy(manifest, target, true);
        }
        Debug.Log("Copied desktop data manifests to " + destination);
    }

    private static void CopyInternationalCodePages(string executablePath)
    {
        string source = Path.Combine(
            EditorApplication.applicationContentsPath,
            "MonoBleedingEdge/lib/mono/4.5");
        string dataDirectory = Path.Combine(
            Path.GetDirectoryName(executablePath),
            Path.GetFileNameWithoutExtension(executablePath) + "_Data");
        string destination = Path.Combine(dataDirectory, "Managed");

        string[] codePageAssemblies = Directory.GetFiles(source, "I18N*.dll");
        if(codePageAssemblies.Length == 0)
            throw new FileNotFoundException("Unity international code-page assemblies were not found", source);
        foreach(string assembly in codePageAssemblies)
            File.Copy(assembly, Path.Combine(destination, Path.GetFileName(assembly)), true);
        Debug.LogFormat("Copied {0} international code-page assemblies to {1}", codePageAssemblies.Length, destination);
    }

    private static void CopyLibVlcRuntime(string executablePath)
    {
        string source = Path.Combine(Application.dataPath, "Scripts/LibVlCSharp/LibVLC/x64");
        string dataDirectory = Path.Combine(
            Path.GetDirectoryName(executablePath),
            Path.GetFileNameWithoutExtension(executablePath) + "_Data");
        string pluginsDirectory = Path.Combine(dataDirectory, "Plugins");
        string destination = Path.Combine(pluginsDirectory, "plugins");

        if(!File.Exists(Path.Combine(source, "libvlc.dll")) ||
            !Directory.Exists(Path.Combine(source, "plugins")))
            throw new DirectoryNotFoundException("LibVLC Windows runtime is incomplete: " + source);

        Directory.CreateDirectory(pluginsDirectory);
        File.Copy(Path.Combine(source, "libvlc.dll"), Path.Combine(pluginsDirectory, "libvlc.dll"), true);
        File.Copy(Path.Combine(source, "libvlccore.dll"), Path.Combine(pluginsDirectory, "libvlccore.dll"), true);
        if(Directory.Exists(destination))
            Directory.Delete(destination, true);
        CopyDirectory(Path.Combine(source, "plugins"), destination);
        Debug.Log("Copied LibVLC Windows runtime to " + destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(string file in Directory.GetFiles(source))
        {
            if(file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach(string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void MoveTmpResources(bool exclude)
    {
        for(int i = 0; i < ExcludedTmpResources.GetLength(0); i++)
        {
            string source = ExcludedTmpResources[i, exclude ? 0 : 1];
            string destination = ExcludedTmpResources[i, exclude ? 1 : 0];
            if(AssetDatabase.LoadMainAssetAtPath(source) == null)
                continue;
            string error = AssetDatabase.MoveAsset(source, destination);
            if(!string.IsNullOrEmpty(error))
                throw new Exception("Unable to move TMP build resource: " + error);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string GetCommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for(int i = 0; i + 1 < args.Length; i++)
            if(args[i] == name)
                return args[i + 1];
        return null;
    }
}
#endif
