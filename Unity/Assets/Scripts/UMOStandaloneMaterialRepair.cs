using System.Reflection;
using UnityEngine;

// Unity 2018 exposes the effective queue publicly, but its serialized override
// (-1 means inherit from the shader) only through this internal getter.
public static class UMOStandaloneMaterialRepair
{
    private static readonly PropertyInfo RawQueue = typeof(Material).GetProperty(
        "rawRenderQueue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static int GetQueueOverride(Material material)
    {
        if(RawQueue != null)
            return (int)RawQueue.GetValue(material, null);
        // Fallback for other Unity versions: don't bake a shader default into
        // the material when replacing an unsupported Android shader.
        return material.shader != null && material.renderQueue == material.shader.renderQueue
            ? -1 : material.renderQueue;
    }

    public static void ReplaceShader(Material material, Shader replacement)
    {
        int queueOverride = GetQueueOverride(material);
        material.shader = replacement;
        material.renderQueue = queueOverride;
    }
}
