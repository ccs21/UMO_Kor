using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Shared by the Unity player and the standalone .NET settings utility.
// Deliberately independent of Unity and of profile/save storage.
public sealed class UMOPcSettings
{
    public const string FileName = "UMO_PC_Settings.ini";
    public string[] Keys4 = { "S", "D", "K", "L" };
    public string[] Keys6 = { "S", "D", "F", "J", "K", "L" };
    public string SkillKey = "Space";
    public string[] Pad4 = { "Left", "Up", "Y", "B" };
    public string[] Pad6 = { "Left", "Up", "Right", "X", "Y", "B" };
    public string[] SkillPad = { "LB", "RB", "LT", "RT" };
    public int AssistMs = 15;
    public int Width = 1280;
    public int Height = 720;
    public int MaxFps = 60;
    public bool LowGraphics = true;
    public bool Fullscreen = false;
    public static readonly string[] PadNames = { "Left", "Up", "Right", "Down", "X", "Y", "B", "A", "LB", "RB", "LT", "RT", "LS", "RS", "Start", "Back" };

    public static string[] KeyNames()
    {
        var names = new List<string>();
        for (char c = 'A'; c <= 'Z'; c++) names.Add(c.ToString());
        for (int n = 0; n <= 9; n++) names.Add("Alpha" + n);
        names.AddRange(new[] { "Space", "LeftShift", "RightShift", "LeftControl", "RightControl", "LeftAlt", "RightAlt", "Return", "Tab", "Backspace", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Comma", "Period", "Slash", "Semicolon", "Quote", "LeftBracket", "RightBracket", "Minus", "Equals", "Keypad0", "Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5", "Keypad6", "Keypad7", "Keypad8", "Keypad9" });
        return names.ToArray();
    }

    public string Validate()
    {
        if (!ValidBindings(Keys4, 4, KeyNames()) || !ValidBindings(Keys6, 6, KeyNames()) || Array.IndexOf(KeyNames(), SkillKey) < 0)
            return "키보드 설정에 지원하지 않는 키 또는 중복 키가 있습니다.";
        if (Array.IndexOf(Keys4, SkillKey) >= 0 || Array.IndexOf(Keys6, SkillKey) >= 0)
            return "스킬 키와 레인 키는 서로 달라야 합니다.";
        if (!ValidBindings(Pad4, 4, PadNames) || !ValidBindings(Pad6, 6, PadNames) || !ValidBindings(SkillPad, SkillPad.Length, PadNames) || SkillPad.Length == 0)
            return "게임패드 설정에 지원하지 않는 버튼 또는 중복 버튼이 있습니다.";
        foreach (string button in SkillPad)
            if (Array.IndexOf(Pad4, button) >= 0 || Array.IndexOf(Pad6, button) >= 0)
                return "스킬 버튼과 레인 버튼은 서로 달라야 합니다.";
        if (AssistMs != 0 && AssistMs != 15 && AssistMs != 30) return "판정 보조는 0, 15, 30ms만 지원합니다.";
        if (!((Width == 1280 && (Height == 720 || Height == 800)) || (Width == 1920 && Height == 1080)))
            return "지원하는 해상도를 선택해 주세요.";
        if (MaxFps != 30 && MaxFps != 60) return "프레임 제한은 30 또는 60입니다.";
        return null;
    }

    private static bool ValidBindings(string[] bindings, int count, string[] allowed)
    {
        if (bindings == null || bindings.Length != count) return false;
        var used = new HashSet<string>();
        foreach (string value in bindings)
            if (Array.IndexOf(allowed, value) < 0 || !used.Add(value)) return false;
        return true;
    }

    public static UMOPcSettings Load(string path)
    {
        var result = new UMOPcSettings();
        if (!File.Exists(path)) return result;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) throw new FormatException("설정 파일 형식 오류: " + line);
            string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
            string[] values = value.Split(',');
            for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
            switch (key)
            {
                case "Keys4": result.Keys4 = values; break;
                case "Keys6": result.Keys6 = values; break;
                case "SkillKey": result.SkillKey = value; break;
                case "Pad4": result.Pad4 = values; break;
                case "Pad6": result.Pad6 = values; break;
                case "SkillPad": result.SkillPad = values; break;
                case "AssistMs": result.AssistMs = int.Parse(value); break;
                case "Width": result.Width = int.Parse(value); break;
                case "Height": result.Height = int.Parse(value); break;
                case "MaxFps": result.MaxFps = int.Parse(value); break;
                case "LowGraphics": result.LowGraphics = bool.Parse(value); break;
                case "Fullscreen": result.Fullscreen = bool.Parse(value); break;
            }
        }
        string error = result.Validate();
        if (error != null) throw new FormatException(error);
        return result;
    }

    public void Save(string path)
    {
        string error = Validate();
        if (error != null) throw new FormatException(error);
        string text = "# UMO PC 설정. 게임을 재시작하면 적용됩니다. 세이브와 무관합니다.\r\n";
        text += "Keys4=" + string.Join(",", Keys4) + "\r\nKeys6=" + string.Join(",", Keys6);
        text += "\r\nSkillKey=" + SkillKey + "\r\nPad4=" + string.Join(",", Pad4) + "\r\nPad6=" + string.Join(",", Pad6);
        text += "\r\nSkillPad=" + string.Join(",", SkillPad) + "\r\nAssistMs=" + AssistMs;
        text += "\r\nWidth=" + Width + "\r\nHeight=" + Height + "\r\nMaxFps=" + MaxFps;
        text += "\r\nLowGraphics=" + LowGraphics + "\r\nFullscreen=" + Fullscreen + "\r\n";
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // InputManager's outer two slots are unused in four-lane play.
    public static int VisibleLane(int slot, int laneCount)
    {
        return laneCount == 6 ? slot : (slot >= 1 && slot <= 4 ? slot - 1 : -1);
    }

    // Aggregate keyboard + pad before computing edges; releasing one device
    // must not release a note that is still held on the other device.
    public static int Transition(bool previous, bool current)
    {
        return current ? (previous ? 2 : 0) : (previous ? 1 : -1);
    }
}
