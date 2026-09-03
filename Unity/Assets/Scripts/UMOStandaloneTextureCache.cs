using System;
using System.IO;
using System.Security.Cryptography;

public static class UMOStandaloneTextureCache
{
    // Cache entries are accepted only when they match the decrypted source bytes.
    // Updating an archive/DLC therefore cannot accidentally reuse an older texture.
    public static byte[] Read(string sourcePath, string dataRoot, byte[] original)
    {
        string root = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string source = Path.GetFullPath(sourcePath);
        if(!source.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return original;
        string cache = Path.Combine(root, "WindowsCache", source.Substring(root.Length));
        string stamp = cache + ".sha256";
        if(!File.Exists(cache) || !File.Exists(stamp))
            return original;
        string expected = File.ReadAllText(stamp).Trim();
        string actual;
        using(var sha = SHA256.Create())
            actual = BitConverter.ToString(sha.ComputeHash(original)).Replace("-", "").ToLowerInvariant();
        if(!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            return original;
        return File.ReadAllBytes(cache);
    }
}
