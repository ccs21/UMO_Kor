using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies only the reviewed Korean image set bundled with a release. There is
/// no external directory, hotkey, or user-provided image loading in this path.
/// </summary>
public static class UMOKoreanImagePatcher
{
    [Serializable]
    private sealed class Manifest { public int version; public ImageEntry[] images; }

    [Serializable]
    private sealed class ImageEntry
    {
        public string file;
        public string resourceId;
        public string textureName;
        public int width;
        public int height;
        public bool builtIn;
        public string[] bundles;
    }

    private sealed class BuiltInHost : MonoBehaviour
    {
        private readonly HashSet<string> applied = new HashSet<string>();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ApplyPeriodically());
        }

        private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ApplyBuiltIn(applied); }

        private IEnumerator ApplyPeriodically()
        {
            while(ApplyBuiltIn(applied) < BuiltInCount())
            {
                yield return new WaitForSecondsRealtime(2f);
            }
            Destroy(gameObject);
        }
    }

    private static Manifest manifest;
    private static bool manifestAttempted;
    private static readonly Dictionary<string, byte[]> pngCache = new Dictionary<string, byte[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureManifest();
        if(manifest == null)
            return;
        var host = new GameObject("UMO Korean Reviewed Images");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<BuiltInHost>();
    }

    public static void ApplyBundle(string bundleName, AssetBundle bundle)
    {
        EnsureManifest();
        if(manifest == null || bundle == null)
            return;
        string normalized = NormalizeBundle(bundleName);
        var matching = new List<ImageEntry>();
        foreach(ImageEntry entry in manifest.images)
        {
            if(!entry.builtIn && ContainsBundle(entry, normalized))
                matching.Add(entry);
        }
        if(matching.Count == 0)
            return;

        Texture2D[] textures = bundle.LoadAllAssets<Texture2D>();
        foreach(ImageEntry entry in matching)
        {
            foreach(Texture2D texture in textures)
            {
                if(Matches(entry, texture))
                {
                    Apply(entry, texture);
                    break;
                }
            }
        }
    }

    private static int ApplyBuiltIn(HashSet<string> applied)
    {
        EnsureManifest();
        if(manifest == null)
            return 0;
        Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
        foreach(ImageEntry entry in manifest.images)
        {
            if(!entry.builtIn || applied.Contains(entry.resourceId))
                continue;
            foreach(Texture2D texture in textures)
            {
                if(Matches(entry, texture) && Apply(entry, texture))
                {
                    applied.Add(entry.resourceId);
                    break;
                }
            }
        }
        return applied.Count;
    }

    private static int BuiltInCount()
    {
        int count = 0;
        foreach(ImageEntry entry in manifest.images)
            if(entry.builtIn)
                count++;
        return count;
    }

    private static bool Apply(ImageEntry entry, Texture2D texture)
    {
        try
        {
            byte[] png;
            if(!pngCache.TryGetValue(entry.resourceId, out png))
            {
                TextAsset asset = Resources.Load<TextAsset>("KoreanImageOverrides/" + entry.resourceId);
                if(asset == null)
                    throw new InvalidOperationException("Bundled Korean PNG is missing: " + entry.file);
                png = asset.bytes;
                pngCache[entry.resourceId] = png;
            }
            if(!texture.LoadImage(png, false))
                throw new InvalidOperationException("Texture2D.LoadImage returned false");
            Debug.Log("[UMO Korean image] applied " + entry.file);
            return true;
        }
        catch(Exception error)
        {
            Debug.LogError("[UMO Korean image] failed " + entry.file + "\n" + error);
            return false;
        }
    }

    private static bool Matches(ImageEntry entry, Texture2D texture)
    {
        return texture != null && texture.name == entry.textureName &&
            texture.width == entry.width && texture.height == entry.height;
    }

    private static bool ContainsBundle(ImageEntry entry, string bundle)
    {
        if(entry.bundles == null)
            return false;
        foreach(string candidate in entry.bundles)
            if(NormalizeBundle(candidate) == bundle)
                return true;
        return false;
    }

    private static string NormalizeBundle(string value)
    {
        return (value ?? "").Replace('\\', '/').TrimStart('/').ToLowerInvariant();
    }

    private static void EnsureManifest()
    {
        if(manifestAttempted)
            return;
        manifestAttempted = true;
        TextAsset asset = Resources.Load<TextAsset>("KoreanImageOverrides/manifest");
        if(asset == null)
        {
            Debug.LogError("[UMO Korean image] manifest is missing");
            return;
        }
        manifest = JsonUtility.FromJson<Manifest>(asset.text);
        if(manifest == null || manifest.images == null)
        {
            manifest = null;
            Debug.LogError("[UMO Korean image] manifest is invalid");
        }
    }
}
