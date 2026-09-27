using UnityEngine;

/// <summary>
/// Supplies the Korean message archive bundled with the Korean edition.
/// Other language packs continue to use the existing DLC path.
/// </summary>
public static class EmbeddedKoreanLocalization
{
    private const string ResourcePath = "Localizations/Database/ko";
    private static XeSys.MessageBank earlyStringLiterals;

    // Download errors can occur before the master database loads its language banks.
    // Read the same bundled translations on demand, without needing game data or DLC.
    public static bool TryTranslateEarlyLiteral(string key, out string translated)
    {
        translated = null;
        if(RuntimeSettings.CurrentSettings.Language != "ko") return false;
        if(earlyStringLiterals == null)
        {
            byte[] bytes;
            if(!TryGetArchive(out bytes)) return false;
            var archive = new CBBJHPBGBAJ_Archive();
            archive.KHEKNNFCAOI_Init(bytes);
            var file = archive.KGHAJGGMPKL_files.Find(entry => entry.OPFGFINHFCE_name.Contains("string_literals.bytes"));
            if(file == null) return false;
            earlyStringLiterals = new XeSys.MessageBank();
            earlyStringLiterals.Setup(file.DBBGALAPFGC_bytes, "string_literals");
        }
        string value = earlyStringLiterals.GetMessageByLabel(key);
        if(value.StartsWith("!not exist [", System.StringComparison.Ordinal)) return false;
        translated = PoFile.UnescapeTranslatedText(value);
        return true;
    }

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
