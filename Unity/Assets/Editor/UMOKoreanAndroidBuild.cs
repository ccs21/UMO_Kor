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
    private const string ParallelTestOutput = "Build/Android/UMO_Kor-parallel-test.apk";
    private const string ParallelTestPackage = "com.ccs21.UMOKorTest";
    public const string ReleasePackage = "com.ccs21.umokor";
    public const string ReleaseVersion = "1.1.16-ko-beta.4";
    public const int ReleaseVersionCode = 10004;
    private const string KoreanProductName = "\uC6B0\uD0C0\uB9C8\uD06C\uB85C\uC2A4";

    public static void BuildAndroid()
    {
        Build(DefaultOutput, null, KoreanProductName);
    }

    public static void BuildParallelTest()
    {
        Build(ParallelTestOutput, ParallelTestPackage, KoreanProductName);
    }

    // Never use a debug certificate for a public beta. Keep this identity and
    // the private signing key for all future in-place Korean edition updates.
    public static void BuildRelease()
    {
        string keyStore = RequireEnvironment("UMO_RELEASE_KEYSTORE");
        RequirePath(keyStore);
        string storePass = Environment.GetEnvironmentVariable("UMO_RELEASE_STORE_PASS");
        string keyPass = Environment.GetEnvironmentVariable("UMO_RELEASE_KEY_PASS");
        string alias = Environment.GetEnvironmentVariable("UMO_RELEASE_KEY_ALIAS");
        if(string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(keyPass) || string.IsNullOrEmpty(alias))
            throw new Exception("Release signing credentials are required; debug signing is forbidden.");
        string oldStore = PlayerSettings.Android.keystoreName;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        string oldVersion = PlayerSettings.bundleVersion;
        int oldCode = PlayerSettings.Android.bundleVersionCode;
        try
        {
            PlayerSettings.Android.keystoreName = keyStore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            PlayerSettings.bundleVersion = ReleaseVersion;
            PlayerSettings.Android.bundleVersionCode = ReleaseVersionCode;
            Build("Build/Android/UMO_Kor-" + ReleaseVersion + ".apk", ReleasePackage, KoreanProductName, BuildOptions.None);
        }
        finally
        {
            PlayerSettings.Android.keystoreName = oldStore;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            PlayerSettings.bundleVersion = oldVersion;
            PlayerSettings.Android.bundleVersionCode = oldCode;
        }
    }

    private static void Build(string defaultOutput, string temporaryPackage, string temporaryProductName, BuildOptions buildOptions = BuildOptions.Development)
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

        string output = GetCommandLineValue("-umoOutput") ?? defaultOutput;
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
            options = buildOptions,
        };

        string originalPackage = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        string originalProductName = PlayerSettings.productName;
        try
        {
            // A Windows build may have moved the stock TMP resources away.
            UMOKoreanWindowsBuild.RestoreResources();
            if (!string.IsNullOrEmpty(temporaryPackage))
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, temporaryPackage);
            if (!string.IsNullOrEmpty(temporaryProductName))
                PlayerSettings.productName = temporaryProductName;

            Resources.Load<ShaderList>("ShaderList").ReloadAssetList();
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.LogFormat("UMO Korean Android build: result={0}, errors={1}, warnings={2}, bytes={3}, output={4}",
                summary.result, summary.totalErrors, summary.totalWarnings, summary.totalSize, output);
            if (summary.result != BuildResult.Succeeded)
                throw new Exception("Android build failed: " + summary.result);
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, originalPackage);
            PlayerSettings.productName = originalProductName;
        }
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
