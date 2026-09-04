#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using XeApp.Game.RhythmGame;

// Isolated rendering test using installed bundles. Does not load a game/profile.
public static class UMONoteEffectProbe
{
    public static void Run()
    {
        RunCase(false);
    }

    public static void RunFixed()
    {
        RunCase(true);
    }

    private static void Repair(GameObject source)
    {
        foreach (var r in source.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/rhythmuivertexcolor.shader");
                if (shader == null || !shader.isSupported) throw new Exception("Missing supported effect shader");
                UMOStandaloneMaterialRepair.ReplaceShader(m, shader);
            }
    }

    private static void RunCase(bool repairBeforeInstantiate)
    {
        string bundlePath = Path.GetFullPath("Build/Windows/UMO_Kor/Data/WindowsCache/android/gm/if/un.xab");
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null) throw new Exception("Cannot load " + bundlePath);
        string path = bundle.GetAllAssetNames().First(x => x.EndsWith("/ui_rhythm_obj.prefab"));
        var prefab = bundle.LoadAsset<GameObject>(path);
        Debug.Log("[EffectProbe] prefab=" + path + " repairBeforeInstantiate=" + repairBeforeInstantiate);
        if (repairBeforeInstantiate) Repair(prefab);
        var instances = new GameObject[6];
        for (int i = 0; i < instances.Length; i++)
        {
            instances[i] = UnityEngine.Object.Instantiate(prefab);
            instances[i].transform.position = new Vector3(i * 1000, 0, 0);
            if (!repairBeforeInstantiate) Repair(instances[i]);
            var controller = instances[i].GetComponent<EffectBundleController>();
            // Awake is not automatic outside play mode.
            controller.Awake();
            controller.Play("UI_rhythm_Push_OUT");
            controller.Play("UI_rhythm_Long_OUT");
        }
        for (int i = 0; i < instances.Length; i++)
        {
            var go = instances[i];
            var controller = go.GetComponent<EffectBundleController>();
            var animator = go.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0);
            controller.Play("UI_rhythm_Push_IN");
            animator.Update(0.05f);
            Inspect(go, i, "push");
            controller.Play("UI_rhythm_Long_Loop");
            animator.Update(0.1f);
            Inspect(go, i, "hold");
            var flash = go.GetComponentsInChildren<Renderer>(true).First(r => r.name == "fx_rhythm_flash02");
            var block = new MaterialPropertyBlock();
            flash.GetPropertyBlock(block);
            float expected = !repairBeforeInstantiate && i == 0 ? 0 : 1;
            if (Mathf.Abs(block.GetFloat("_Alpha") - expected) > 0.001f)
                throw new Exception("Unexpected flash alpha on lane " + i);
        }
        foreach (var go in instances) UnityEngine.Object.DestroyImmediate(go);
        bundle.Unload(true);
        Debug.Log("[EffectProbe] PASS " + (repairBeforeInstantiate ? "all six clones have animated alpha=1" : "first-clone alpha=0 failure reproduced"));
    }

    private static void Inspect(GameObject go, int lane, string action)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            string description = "[EffectProbe] lane=" + lane + " action=" + action + " renderer=" + r.name +
                " enabled=" + r.enabled + " active=" + r.gameObject.activeInHierarchy + " bounds=" + r.bounds;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            description += " blockEmpty=" + block.isEmpty + " blockAlpha=" + block.GetFloat("_Alpha");
            foreach (var m in r.sharedMaterials)
                if (m != null) description += " mat=" + m.GetInstanceID() + " shader=" + m.shader.name +
                    " supported=" + m.shader.isSupported + " alpha=" + (m.HasProperty("_Alpha") ? m.GetFloat("_Alpha") : -1) +
                    " tex=" + (m.mainTexture == null ? "null" : m.mainTexture.name);
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                description += " mesh=" + mf.sharedMesh.GetInstanceID() + " vertices=" + mf.sharedMesh.vertexCount;
            Debug.Log(description);
        }
    }
}
#endif
