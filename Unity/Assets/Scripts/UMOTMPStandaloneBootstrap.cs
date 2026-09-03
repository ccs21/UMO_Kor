#if UNITY_STANDALONE
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

// Unity 2018's Windows resource serializer rejects the package-based TMP
// ScriptableObject resources in this recovered project. Supply equivalent
// runtime settings before the first scene instead.
public static class UMOTMPStandaloneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        TMP_Settings settings = ScriptableObject.CreateInstance<UMOTMPSettings>();
        settings.hideFlags = HideFlags.HideAndDontSave;

        TMP_StyleSheet styleSheet = ScriptableObject.CreateInstance<UMOTMPStyleSheet>();
        styleSheet.hideFlags = HideFlags.HideAndDontSave;

        Set(settings, "m_enableWordWrapping", true);
        Set(settings, "m_enableKerning", true);
        Set(settings, "m_enableParseEscapeCharacters", true);
        Set(settings, "m_EnableRaycastTarget", true);
        Set(settings, "m_GetFontFeaturesAtRuntime", true);
        Set(settings, "m_defaultFontAsset", Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"));
        Set(settings, "m_defaultFontAssetPath", "Fonts & Materials/");
        Set(settings, "m_defaultSpriteAssetPath", "Sprite Assets/");
        Set(settings, "m_defaultColorGradientPresetsPath", "Color Gradient Presets/");
        Set(settings, "m_fallbackFontAssets", new List<TMP_FontAsset>());
        Set(settings, "m_defaultStyleSheet", styleSheet);
        Set(settings, "m_leadingCharacters", Resources.Load<TextAsset>("LineBreaking Leading Characters"));
        Set(settings, "m_followingCharacters", Resources.Load<TextAsset>("LineBreaking Following Characters"));

        typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, settings);
        typeof(TMP_StyleSheet).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, styleSheet);
    }

    private static void Set<T>(TMP_Settings settings, string fieldName, T value)
    {
        FieldInfo field = typeof(TMP_Settings).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if(field != null)
            field.SetValue(settings, value);
    }
}
#endif
