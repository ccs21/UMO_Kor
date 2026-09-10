#if UNITY_STANDALONE_WIN && !UNITY_EDITOR && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Captures only textures referenced by currently visible PC UI/renderers.
/// F8 dumps the current screen; F9 reloads user-edited override PNG files.
/// Android builds are not affected.
/// </summary>
public sealed class UMOStandaloneImageTranslationDumper : MonoBehaviour
{
    private sealed class TextureRecord
    {
        public int InstanceId;
        public string Key;
        public string FileName;
        public string TextureName;
        public int Width;
        public int Height;
        public string Sha256;
        public byte[] OriginalPng;
    }

    private sealed class VisibleTexture
    {
        public Texture Texture;
        public readonly HashSet<string> Uses = new HashSet<string>();
    }

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private readonly Dictionary<int, TextureRecord> records = new Dictionary<int, TextureRecord>();
    private readonly Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> applied = new HashSet<int>();
    private string rootDirectory;
    private string originalsDirectory;
    private string overridesDirectory;
    private string screensDirectory;
    private string notice;
    private float noticeUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        var host = new GameObject("UMO PC Image Translation Dumper");
        DontDestroyOnLoad(host);
        host.AddComponent<UMOStandaloneImageTranslationDumper>();
    }

    private void Awake()
    {
        rootDirectory = Path.Combine(Application.dataPath, "..", "ImageTranslation");
        rootDirectory = Path.GetFullPath(rootDirectory);
        originalsDirectory = Path.Combine(rootDirectory, "Originals");
        overridesDirectory = Path.Combine(rootDirectory, "Overrides");
        screensDirectory = Path.Combine(rootDirectory, "Screens");
        Directory.CreateDirectory(originalsDirectory);
        Directory.CreateDirectory(overridesDirectory);
        Directory.CreateDirectory(screensDirectory);
        ReloadOverrideIndex();
        StartCoroutine(ApplyOverridesLoop());
        ShowNotice("이미지 덤프 F8 / 수정 이미지 다시 불러오기 F9", 7f);
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F8))
            StartCoroutine(DumpCurrentScreen());
        if(Input.GetKeyDown(KeyCode.F9))
        {
            ReloadOverrideIndex();
            applied.Clear();
            int count = ApplyOverridesToVisibleTextures();
            ShowNotice("수정 이미지 " + count + "개를 다시 불러왔습니다.", 5f);
        }
    }

    private IEnumerator ApplyOverridesLoop()
    {
        while(true)
        {
            if(overrides.Count > 0)
                ApplyOverridesToVisibleTextures();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private IEnumerator DumpCurrentScreen()
    {
        yield return new WaitForEndOfFrame();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string sceneName = SafeName(SceneManager.GetActiveScene().name);
        string screenDirectory = Path.Combine(screensDirectory, stamp + "_" + sceneName);
        Directory.CreateDirectory(screenDirectory);
        ScreenCapture.CaptureScreenshot(Path.Combine(screenDirectory, "화면.png"));

        Dictionary<int, VisibleTexture> visible = CollectVisibleTextures();
        var manifest = new StringBuilder();
        manifest.AppendLine("file_name\ttexture_name\twidth\theight\tsha256\tuses");
        int saved = 0;
        int failed = 0;
        foreach(var entry in visible.Values)
        {
            TextureRecord record;
            if(!TryGetRecord(entry.Texture, out record))
            {
                failed++;
                continue;
            }
            string originalPath = Path.Combine(originalsDirectory, record.FileName);
            if(!File.Exists(originalPath))
            {
                File.WriteAllBytes(originalPath, record.OriginalPng);
                saved++;
            }
            // Keep only the small identity record after the PNG has reached disk.
            // Touring many screens must not retain every dumped atlas in memory.
            record.OriginalPng = null;
            manifest.Append(EscapeTsv(record.FileName)).Append('\t')
                .Append(EscapeTsv(record.TextureName)).Append('\t')
                .Append(record.Width).Append('\t').Append(record.Height).Append('\t')
                .Append(record.Sha256).Append('\t')
                .Append(EscapeTsv(string.Join(" | ", new List<string>(entry.Uses).ToArray())))
                .AppendLine();
        }
        File.WriteAllText(Path.Combine(screenDirectory, "화면에서_사용된_이미지.tsv"), manifest.ToString(), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(screenDirectory, "사용법.txt"),
            "Originals 폴더에서 번역할 PNG만 Overrides 폴더로 복사해 수정하세요.\r\n" +
            "PNG의 파일명과 크기, 투명 영역을 유지한 뒤 게임에서 F9를 누르면 즉시 반영됩니다.\r\n" +
            "F8을 다시 눌러도 기존 Originals와 Overrides 파일은 덮어쓰지 않습니다.\r\n",
            new UTF8Encoding(true));
        ShowNotice("현재 화면 이미지 " + visible.Count + "개 확인 / 새 원본 " + saved + "개 저장" +
            (failed > 0 ? " / 실패 " + failed + "개" : ""), 7f);
        Debug.Log("[UMO image dump] scene=" + SceneManager.GetActiveScene().name + " visible=" + visible.Count +
            " saved=" + saved + " failed=" + failed + " output=" + screenDirectory);
    }

    private Dictionary<int, VisibleTexture> CollectVisibleTextures()
    {
        var result = new Dictionary<int, VisibleTexture>();
        foreach(var graphic in Resources.FindObjectsOfTypeAll<Graphic>())
        {
            if(graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy ||
                graphic.canvasRenderer == null || graphic.canvasRenderer.cull || graphic.canvasRenderer.GetAlpha() <= 0.001f)
                continue;
            string use = "UI:" + HierarchyPath(graphic.transform);
            AddTexture(result, graphic.mainTexture, use + ":mainTexture");
            AddMaterialTextures(result, graphic.materialForRendering, use);
            var image = graphic as Image;
            if(image != null && image.sprite != null)
                AddTexture(result, image.sprite.texture, use + ":sprite=" + image.sprite.name + ":rect=" + RectText(image.sprite.rect));
            var rawImage = graphic as RawImage;
            if(rawImage != null)
                AddTexture(result, rawImage.texture, use + ":rawImage");
        }
        foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if(renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.isVisible)
                continue;
            string use = "Renderer:" + HierarchyPath(renderer.transform);
            var spriteRenderer = renderer as SpriteRenderer;
            if(spriteRenderer != null && spriteRenderer.sprite != null)
                AddTexture(result, spriteRenderer.sprite.texture,
                    use + ":sprite=" + spriteRenderer.sprite.name + ":rect=" + RectText(spriteRenderer.sprite.rect));
            foreach(var material in renderer.sharedMaterials)
                AddMaterialTextures(result, material, use);
        }
        return result;
    }

    private static void AddMaterialTextures(Dictionary<int, VisibleTexture> result, Material material, string use)
    {
        if(material == null)
            return;
        string[] properties;
        try { properties = material.GetTexturePropertyNames(); }
        catch { properties = new[] { "_MainTex" }; }
        foreach(string property in properties)
        {
            Texture texture = null;
            try { if(material.HasProperty(property)) texture = material.GetTexture(property); }
            catch { }
            AddTexture(result, texture, use + ":material=" + material.name + ":property=" + property);
        }
    }

    private static void AddTexture(Dictionary<int, VisibleTexture> result, Texture texture, string use)
    {
        if(texture == null || texture.width <= 1 || texture.height <= 1)
            return;
        int id = texture.GetInstanceID();
        VisibleTexture entry;
        if(!result.TryGetValue(id, out entry))
        {
            entry = new VisibleTexture { Texture = texture };
            result.Add(id, entry);
        }
        entry.Uses.Add(use);
    }

    private bool TryGetRecord(Texture source, out TextureRecord record)
    {
        int id = source.GetInstanceID();
        if(records.TryGetValue(id, out record))
            return true;
        Texture2D readable = null;
        RenderTexture temporary = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            readable.Apply(false, false);
            byte[] raw = readable.GetRawTextureData();
            string hash;
            using(var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
            string textureName = string.IsNullOrEmpty(source.name) ? "unnamed" : source.name;
            string key = SafeName(textureName) + "__" + source.width + "x" + source.height;
            record = new TextureRecord {
                InstanceId = id,
                Key = key,
                FileName = key + "__" + hash.Substring(0, 16) + ".png",
                TextureName = textureName,
                Width = source.width,
                Height = source.height,
                Sha256 = hash,
                OriginalPng = readable.EncodeToPNG(),
            };
            records.Add(id, record);
            return true;
        }
        catch(Exception error)
        {
            Debug.LogWarning("[UMO image dump] texture read failed name=" + source.name + " error=" + error);
            record = null;
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            if(temporary != null) RenderTexture.ReleaseTemporary(temporary);
            if(readable != null) Destroy(readable);
        }
    }

    private void ReloadOverrideIndex()
    {
        overrides.Clear();
        Directory.CreateDirectory(overridesDirectory);
        foreach(string path in Directory.GetFiles(overridesDirectory, "*.png", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            overrides[name] = path;
        }
        Debug.Log("[UMO image override] indexed=" + overrides.Count + " directory=" + overridesDirectory);
    }

    private int ApplyOverridesToVisibleTextures()
    {
        int count = 0;
        foreach(var entry in CollectVisibleTextures().Values)
        {
            Texture2D texture = entry.Texture as Texture2D;
            if(texture == null || applied.Contains(texture.GetInstanceID()))
                continue;
            string baseKey = SafeName(string.IsNullOrEmpty(texture.name) ? "unnamed" : texture.name) +
                "__" + texture.width + "x" + texture.height;
            bool possibleOverride = false;
            foreach(string overrideName in overrides.Keys)
            {
                if(overrideName.StartsWith(baseKey + "__", StringComparison.OrdinalIgnoreCase))
                {
                    possibleOverride = true;
                    break;
                }
            }
            if(!possibleOverride)
                continue;
            TextureRecord record;
            if(!TryGetRecord(texture, out record))
                continue;
            string path;
            bool hasOverride = overrides.TryGetValue(Path.GetFileNameWithoutExtension(record.FileName), out path);
            record.OriginalPng = null;
            if(!hasOverride)
                continue;
            try
            {
                byte[] png = File.ReadAllBytes(path);
                if(!texture.LoadImage(png, false))
                    throw new InvalidDataException("Texture2D.LoadImage returned false");
                if(texture.width != record.Width || texture.height != record.Height)
                    Debug.LogWarning("[UMO image override] size changed file=" + path + " expected=" +
                        record.Width + "x" + record.Height + " actual=" + texture.width + "x" + texture.height);
                applied.Add(texture.GetInstanceID());
                count++;
                Debug.Log("[UMO image override] applied texture=" + record.TextureName + " file=" + path);
            }
            catch(Exception error)
            {
                Debug.LogError("[UMO image override] failed file=" + path + " error=" + error);
            }
        }
        return count;
    }

    private void ShowNotice(string text, float seconds)
    {
        notice = text;
        noticeUntil = Time.realtimeSinceStartup + seconds;
    }

    private void OnGUI()
    {
        if(string.IsNullOrEmpty(notice) || Time.realtimeSinceStartup > noticeUntil)
            return;
        var style = new GUIStyle(GUI.skin.box) {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(18, Mathf.RoundToInt(Screen.height / 45f)),
            wordWrap = true,
        };
        float width = Mathf.Min(Screen.width - 40, 900);
        GUI.Box(new Rect((Screen.width - width) / 2, 20, width, 62), notice, style);
    }

    private static string SafeName(string value)
    {
        if(string.IsNullOrEmpty(value))
            return "unnamed";
        var builder = new StringBuilder(value.Length);
        foreach(char character in value)
            builder.Append(Array.IndexOf(InvalidFileNameChars, character) >= 0 ? '_' : character);
        string result = builder.ToString().Trim(' ', '.');
        if(result.Length > 90)
            result = result.Substring(0, 90);
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    private static string EscapeTsv(string value)
    {
        return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    }

    private static string RectText(Rect rect)
    {
        return rect.x + "," + rect.y + "," + rect.width + "," + rect.height;
    }

    private static string HierarchyPath(Transform node)
    {
        string path = node.name;
        while(node.parent != null)
        {
            node = node.parent;
            path = node.name + "/" + path;
        }
        return path;
    }
}
#endif
