#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class UMOKoreanAndroidBuild
{
    private const string DefaultOutput = "Build/Android/UMO_Kor-debug.apk";

    public static void BuildAndroid()
    {
        ConfigureToolchain();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new Exception("Unable to switch the active build target to Android.");

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new Exception("No enabled scenes were found in EditorBuildSettings.");

        string output = GetCommandLineValue("-umoOutput") ?? DefaultOutput;
        if (!Path.IsPathRooted(output))
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.buildAppBundle = false;
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.LogFormat("UMO Korean Android build: result={0}, errors={1}, warnings={2}, bytes={3}, output={4}",
            summary.result, summary.totalErrors, summary.totalWarnings, summary.totalSize, output);
        if (summary.result != BuildResult.Succeeded)
            throw new Exception("Android build failed: " + summary.result);
    }

    private static void ConfigureToolchain()
    {
        string sdk = RequireEnvironment("UMO_ANDROID_SDK");
        string ndk = RequireEnvironment("UMO_ANDROID_NDK");
        string jdk = RequireEnvironment("UMO_JAVA_HOME");
        RequirePath(Path.Combine(sdk, "platforms", "android-28", "android.jar"));
        RequirePath(Path.Combine(ndk, "ndk-build.cmd"));
        RequirePath(Path.Combine(jdk, "bin", "javac.exe"));
        EditorPrefs.SetString("AndroidSdkRoot", sdk);
        EditorPrefs.SetString("AndroidNdkRootR16b", ndk);
        EditorPrefs.SetString("JdkPath", jdk);
    }

    private static string RequireEnvironment(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
            throw new Exception("Missing environment variable: " + name);
        return Path.GetFullPath(value);
    }

    private static void RequirePath(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Required Android build tool was not found.", path);
    }

    private static string GetCommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
#endif
