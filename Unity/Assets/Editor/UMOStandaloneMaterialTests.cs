#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class UMOStandaloneMaterialTests
{
    public static void PrepareAndTest()
    {
        UMOKoreanWindowsBuild.PrepareResources();
        Resources.Load<ShaderList>("ShaderList").ReloadAssetList();
        foreach(string name in new[] { "MCRS/Diva/Trans_Eye", "MCRS/SplitTextureRGB16A8", "Unlit/Texture", "UI/Default", "UMO/PC/TransparentColoredBlur" })
        {
            Shader shader = Shader.Find(name);
            if(shader == null) throw new Exception("Missing test shader: " + name);
            var material = new Material(shader);
            Debug.Log("[UMO shader test] " + name + " supported=" + shader.isSupported + " queue=" + shader.renderQueue +
                " QueueTag=" + material.GetTag("Queue", false) + " UpperQueueTag=" + material.GetTag("QUEUE", false));
            material.renderQueue = -1;
            UMOStandaloneMaterialRepair.ReplaceShader(material, Shader.Find("UI/Default"));
            if(UMOStandaloneMaterialRepair.GetQueueOverride(material) != -1 || material.renderQueue != material.shader.renderQueue)
                throw new Exception("Inherited material queue was not preserved");
            material.renderQueue = 3107;
            UMOStandaloneMaterialRepair.ReplaceShader(material, shader);
            if(material.renderQueue != 3107) throw new Exception("Explicit material queue was not preserved");
            UnityEngine.Object.DestroyImmediate(material);
        }
        Debug.Log("[UMO shader test] PASS inherited and explicit queue preservation");
    }
}
#endif
