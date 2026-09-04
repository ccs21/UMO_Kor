#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XeApp.Game.RhythmGame.UI;

// Temporary, bounded read-only comparison of the first lane and its peers.
public sealed class UMOPcNoteEffectTrace : MonoBehaviour
{
    private readonly Dictionary<string, int> samples = new Dictionary<string, int>();

    public static void Capture(TouchPrefabInstance effect, int track, string action)
    {
        if (effect == null) { Debug.LogWarning("[UMO note effect] missing track=" + track); return; }
        var trace = effect.GetComponent<UMOPcNoteEffectTrace>();
        if (trace == null) trace = effect.gameObject.AddComponent<UMOPcNoteEffectTrace>();
        int count;
        trace.samples.TryGetValue(action, out count);
        if (count >= 2 || !trace.isActiveAndEnabled) return;
        trace.samples[action] = count + 1;
        trace.StartCoroutine(trace.Sample(effect, track, action));
    }

    private IEnumerator Sample(TouchPrefabInstance effect, int track, string action)
    {
        Debug.Log("[UMO note effect request] track=" + track + " action=" + action + " frame=" + Time.frameCount +
            " owner=" + effect.FingerId + " world=" + effect.transform.position + " scale=" + effect.transform.lossyScale);
        yield return new WaitForEndOfFrame();
        if (effect == null || effect.touchEffect == null) yield break;
        var b = new StringBuilder("[UMO note effect rendered] track=" + track + " action=" + action + " frame=" + Time.frameCount);
        var canvas = effect.GetComponentInParent<Canvas>();
        var camera = canvas == null ? null : canvas.worldCamera;
        if (camera != null) b.Append(" viewport=").Append(camera.WorldToViewportPoint(effect.transform.position));
        foreach (var group in effect.touchEffect.m_params)
        {
            b.Append("\n group=").Append(group.groupName);
            foreach (var item in group.Animations)
            {
                var a = item.animator;
                if (a == null) { b.Append(" animator=null"); continue; }
                b.Append(" anim=").Append(a.name).Append(" id=").Append(a.GetInstanceID())
                    .Append(" owned=").Append(a.transform.IsChildOf(effect.transform))
                    .Append(" active=").Append(a.gameObject.activeInHierarchy).Append(" enabled=").Append(a.enabled)
                    .Append(" culling=").Append(a.cullingMode);
                if (a.isActiveAndEnabled && a.runtimeAnimatorController != null && item.layerIndex < a.layerCount)
                    b.Append(" state=").Append(a.GetCurrentAnimatorStateInfo(item.layerIndex).shortNameHash);
            }
            foreach (var item in group.particles)
                if (item.particle != null) b.Append(" particle=").Append(item.particle.name)
                    .Append(" owned=").Append(item.particle.transform.IsChildOf(effect.transform))
                    .Append(" playing=").Append(item.particle.isPlaying).Append(" count=").Append(item.particle.particleCount);
        }
        foreach (var r in effect.touchEffect.GetComponentsInChildren<Renderer>(true))
        {
            b.Append("\n renderer=").Append(r.name).Append(" active=").Append(r.gameObject.activeInHierarchy)
                .Append(" enabled=").Append(r.enabled).Append(" visible=").Append(r.isVisible)
                .Append(" layer=").Append(r.gameObject.layer).Append(" bounds=").Append(r.bounds);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            b.Append(" blockEmpty=").Append(block.isEmpty).Append(" animatedAlpha=").Append(block.GetFloat("_Alpha"));
            foreach (var m in r.sharedMaterials)
                if (m != null) b.Append(" material=").Append(m.name).Append(" shader=").Append(m.shader == null ? "null" : m.shader.name)
                    .Append(" queue=").Append(m.renderQueue).Append(" materialAlpha=").Append(m.HasProperty("_Alpha") ? m.GetFloat("_Alpha") : -1);
        }
        Debug.Log(b.ToString());
    }
}
#endif
