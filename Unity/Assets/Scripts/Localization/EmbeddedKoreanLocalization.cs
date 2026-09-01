using UnityEngine;

/// <summary>
/// Supplies the Korean message archive bundled with the Korean edition.
/// Other language packs continue to use the existing DLC path.
/// </summary>
public static class EmbeddedKoreanLocalization
{
    private const string ResourcePath = "Localizations/Database/ko";

    public static bool TryGetArchive(out byte[] bytes)
    {
        bytes = null;
        if (RuntimeSettings.CurrentSettings.Language != "ko")
            return false;

        TextAsset archive = Resources.Load<TextAsset>(ResourcePath);
        if (archive == null)
            return false;

        bytes = archive.bytes;
        return bytes != null && bytes.Length > 0;
    }
}
