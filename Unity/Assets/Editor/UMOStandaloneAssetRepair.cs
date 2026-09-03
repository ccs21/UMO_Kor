#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UMOStandaloneAssetRepair
{
    private static readonly string[] PrefabsWithKnownMissingScripts =
    {
        "Assets/UMAssets/Resources/menu/prefab/UI_TeamSelect.prefab",
        "Assets/UMAssets/Resources/menu/prefab/popupwindow/PopupClearReward.prefab",
        "Assets/UMAssets/Resources/menu/prefab/popupwindow/PopupMissionReward.prefab",
    };

    private static readonly string[] TextMeshProResources =
    {
        "Assets/TextMesh Pro/Resources/TMP Settings.asset",
        "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset",
        "Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset",
    };

    private static readonly string[] AndroidCompressedStandaloneTextures =
    {
        "Assets/Popup/cmn_config_packtex_mask.asset",
        "Assets/Popup/cmn_tex_pack_mask.asset",
    };

    [MenuItem("UMO/Repair standalone build assets")]
    public static void Repair()
    {
        int removed = 0;
        foreach(string path in PrefabsWithKnownMissingScripts)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if(count == 0)
                        continue;
                    removed += count;
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Re-serialize the bundled TMP essentials against the installed TMP
        // package. Their original YAML predates the package-manager version.
        AssetDatabase.ForceReserializeAssets(new List<string>(TextMeshProResources));
        foreach(string path in AndroidCompressedStandaloneTextures)
            ConvertToRgba32(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.LogFormat("UMO standalone asset repair: removedMissingScripts={0}", removed);
    }

    private static void ConvertToRgba32(string path)
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(source == null || source.format == TextureFormat.RGBA32)
            return;

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D decoded = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            decoded = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            decoded.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            decoded.Apply(false, false);
            decoded.name = source.name;
            decoded.filterMode = source.filterMode;
            decoded.wrapMode = source.wrapMode;
            EditorUtility.CopySerialized(decoded, source);
            EditorUtility.SetDirty(source);
            Debug.LogFormat("UMO standalone texture conversion: path={0}, format=RGBA32", path);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if(decoded != null)
                Object.DestroyImmediate(decoded);
        }
    }

    public static void InspectTextMeshProResources()
    {
        foreach(string path in TextMeshProResources)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            Debug.LogFormat("UMO TMP resource: path={0}, object={1}, type={2}", path,
                asset == null ? "<null>" : asset.name,
                asset == null ? "<null>" : asset.GetType().AssemblyQualifiedName);
        }

        string[] scripts =
        {
            "Assets/Scripts/UMOTMPSettings.cs",
            "Assets/Scripts/UMOTMPSpriteAsset.cs",
            "Assets/Scripts/UMOTMPStyleSheet.cs",
        };
        foreach(string path in scripts)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            System.Type type = script == null ? null : script.GetClass();
            Debug.LogFormat("UMO TMP script: path={0}, class={1}", path,
                type == null ? "<null>" : type.AssemblyQualifiedName);
        }
    }
}
#endif
