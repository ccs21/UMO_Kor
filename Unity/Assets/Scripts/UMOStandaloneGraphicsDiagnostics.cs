#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Opt-in, read-only graphics inventory. Never writes profiles or save data.
public class UMOStandaloneGraphicsDiagnostics : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(), "-umoGraphicsDiagnostics") < 0)
            return;
        var host = new GameObject("UMO PC Graphics Diagnostics");
        DontDestroyOnLoad(host);
        host.AddComponent<UMOStandaloneGraphicsDiagnostics>();
    }

    private IEnumerator Start()
    {
        for(int sample = 0; sample < 40; sample++)
        {
            yield return new WaitForSecondsRealtime(15);
            DumpMenuWaits();
            var counts = new Dictionary<string, int>();
            foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                string key = texture.format + "/supported=" + SystemInfo.SupportsTextureFormat(texture.format);
                if(!counts.ContainsKey(key)) counts[key] = 0;
                counts[key]++;
                if(!SystemInfo.SupportsTextureFormat(texture.format) || texture.name == "0050" || texture.name == "150001_base" || texture.name == "adv_login_pack_base" || texture.name == "adv_login_pack_mask")
                    Debug.Log("[UMO PC graphics] texture=" + texture.name + " " + key + " " + texture.width + "x" + texture.height + " readable=" + texture.isReadable);
            }
            foreach(var count in counts)
                Debug.Log("[UMO PC graphics] texture-count " + count.Key + "=" + count.Value);
            int unsupported = 0, supported = 0, details = 0;
            foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if(material.shader == null) continue;
                if(material.shader.isSupported) supported++; else unsupported++;
                if(!material.shader.isSupported || (details < 12 && material.shader.name.Contains("SplitTexture")))
                {
                    Debug.Log("[UMO PC graphics] material=" + material.name + " shader=" + material.shader.name + " supported=" + material.shader.isSupported);
                    details++;
                }
            }
            Debug.Log("[UMO PC graphics] materials supported=" + supported + " unsupported=" + unsupported);
            foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if(!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material == null || material.shader == null) continue;
                    if(material.name.Contains("eye_mat") || material.name.Contains("battle_BG") || !material.shader.isSupported)
                        Debug.Log("[UMO PC renderer] " + renderer.name + " material=" + material.name +
                            " shader=" + material.shader.name + " supported=" + material.shader.isSupported +
                            " queue=" + material.renderQueue + " override=" + UMOStandaloneMaterialRepair.GetQueueOverride(material) +
                            " texture=" + (material.mainTexture == null ? "null" : material.mainTexture.name));
                }
            }
        }
    }

    // Read-only snapshots: locate a stalled transition without skipping tutorial
    // steps, unlocking inputs or changing the player's progress.
    private static void DumpMenuWaits()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var menu = XeApp.Game.Menu.MenuScene.Instance;
        if(menu != null && menu.FooterMenu != null)
        {
            Debug.Log("[UMO PC menu input] inputBlocks=" + menu.GetInputDisableCount() + " raycastBlocks=" + menu.GetRaycastDisableCount());
            var button = menu.FooterMenu.FindButton(XeApp.Game.Menu.MenuFooterControl.Button.Mission);
            if(button != null)
            {
                Debug.Log("[UMO PC mission button] " + HierarchyPath(button.transform) + " active=" + button.gameObject.activeInHierarchy +
                    " enabled=" + button.enabled + " off=" + button.IsInputOff + " locked=" + button.IsInputLock + " disabled=" + button.Disable + " hidden=" + button.Hidden);
                var hit = button.GetComponentInChildren<XeSys.Gfx.LayoutUGUIHitOnly>(true);
                if(hit != null)
                {
                    Debug.Log("[UMO PC mission hit] enabled=" + hit.enabled + " raycast=" + hit.raycastTarget + " rect=" + hit.rectTransform.rect);
                    var canvas = hit.GetComponentInParent<Canvas>();
                    var center = RectTransformUtility.WorldToScreenPoint(canvas.rootCanvas.worldCamera, hit.rectTransform.TransformPoint(hit.rectTransform.rect.center));
                    var system = UnityEngine.EventSystems.EventSystem.current;
                    if(system != null)
                    {
                        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                        system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system) { position = center }, hits);
                        for(int n = 0; n < Math.Min(hits.Count, 4); n++)
                            Debug.Log("[UMO PC mission raycast] " + n + " " + HierarchyPath(hits[n].gameObject.transform));
                    }
                }
            }
        }
        foreach(var watcher in Resources.FindObjectsOfTypeAll<CoroutineWatcher>())
        {
            foreach(var info in watcher.coroutines.ToArray())
            {
                var routine = info.originalEnumerator;
                if(routine == null) continue;
                string name = routine.GetType().FullName;
                if(!(name.Contains("Home") || name.Contains("Tutorial") || name.Contains("Transition") ||
                    name.Contains("InOutAnime") || name.Contains("Popup"))) continue;
                var state = new System.Text.StringBuilder();
                foreach(var field in routine.GetType().GetFields(flags))
                {
                    if(field.FieldType == typeof(int) || field.FieldType == typeof(bool) || field.FieldType == typeof(float))
                        state.Append(" ").Append(field.Name).Append("=").Append(field.GetValue(routine));
                }
                Debug.Log("[UMO PC menu wait] " + name + state);
            }
        }
        foreach(var anim in Resources.FindObjectsOfTypeAll<XeApp.Game.Common.InOutAnime>())
        {
            if(!anim.gameObject.scene.IsValid()) continue;
            var rect = anim.transform as RectTransform;
            var state = new System.Text.StringBuilder();
            foreach(string name in new[] { "state", "inType", "animTime", "moveAmount", "m_enterPos", "m_leavePos", "m_isInit" })
            {
                var field = anim.GetType().GetField(name, flags);
                if(field != null) state.Append(" ").Append(name).Append("=").Append(field.GetValue(anim));
            }
            Debug.Log("[UMO PC menu animation] " + HierarchyPath(anim.transform) +
                " active=" + anim.gameObject.activeInHierarchy + " playing=" + anim.IsPlaying() +
                " position=" + (rect == null ? Vector2.zero : rect.anchoredPosition) + state);
        }
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
