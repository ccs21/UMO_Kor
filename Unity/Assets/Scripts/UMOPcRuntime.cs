#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class UMOPcRuntime
{
    private static UMOPcSettings settings;
    public static UMOPcSettings Settings
    {
        get
        {
            if (settings == null)
            {
                try { settings = UMOPcSettings.Load(Path.GetFullPath(Path.Combine(Application.dataPath, "../" + UMOPcSettings.FileName))); }
                catch (Exception e) { Debug.LogWarning("PC settings invalid; using defaults: " + e.Message); settings = new UMOPcSettings(); }
                Debug.Log("UMO PC settings: assist=" + settings.AssistMs + "ms; " + settings.Width + "x" + settings.Height + "; fps=" + settings.MaxFps);
            }
            return settings;
        }
    }

    private static KeyCode[] keys4, keys6;
    private static KeyCode skill;
    private static readonly bool[] held = new bool[7];
    private static int padFrame = -1;
    private static PadState pad;
    private static bool nativeUnavailable;
    private static int padIndex = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PadState
    {
        public uint packet;
        public ushort buttons;
        public byte leftTrigger, rightTrigger;
        public short lx, ly, rx, ry;
    }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint GetState(uint index, out PadState state);

    private static KeyCode[] ParseKeys(string[] names)
    {
        var result = new KeyCode[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = (KeyCode)Enum.Parse(typeof(KeyCode), names[i]);
        return result;
    }

    private static void PollPad()
    {
        if (padFrame == Time.frameCount) return;
        padFrame = Time.frameCount;
        pad = new PadState();
        if (nativeUnavailable || !Application.isFocused) return;
        try
        {
            if (padIndex >= 0 && GetState((uint)padIndex, out pad) == 0) return;
            padIndex = -1;
            for (uint i = 0; i < 4; i++)
                if (GetState(i, out pad) == 0) { padIndex = (int)i; return; }
            pad = new PadState();
        }
        catch (DllNotFoundException) { nativeUnavailable = true; }
        catch (EntryPointNotFoundException) { nativeUnavailable = true; }
    }

    private static bool PadPressed(string name)
    {
        if (name == "LT") return pad.leftTrigger > 30;
        if (name == "RT") return pad.rightTrigger > 30;
        int mask;
        switch (name)
        {
            case "Up": mask = 0x1; break; case "Down": mask = 0x2; break;
            case "Left": mask = 0x4; break; case "Right": mask = 0x8; break;
            case "Start": mask = 0x10; break; case "Back": mask = 0x20; break;
            case "LS": mask = 0x40; break; case "RS": mask = 0x80; break;
            case "LB": mask = 0x100; break; case "RB": mask = 0x200; break;
            case "A": mask = 0x1000; break; case "B": mask = 0x2000; break;
            case "X": mask = 0x4000; break; case "Y": mask = 0x8000; break;
            default: return false;
        }
        return (pad.buttons & mask) != 0;
    }

    public static int ReadTransition(int slot, int laneCount)
    {
        if (keys4 == null)
        {
            keys4 = ParseKeys(Settings.Keys4); keys6 = ParseKeys(Settings.Keys6);
            skill = (KeyCode)Enum.Parse(typeof(KeyCode), Settings.SkillKey);
        }
        PollPad();
        bool down = false;
        if (Application.isFocused)
        {
            if (slot == 6)
            {
                down = Input.GetKey(skill);
                foreach (string button in Settings.SkillPad) down |= PadPressed(button);
            }
            else
            {
                int lane = UMOPcSettings.VisibleLane(slot, laneCount);
                if (lane >= 0)
                    down = Input.GetKey(laneCount == 6 ? keys6[lane] : keys4[lane]) || PadPressed(laneCount == 6 ? Settings.Pad6[lane] : Settings.Pad4[lane]);
            }
        }
        int transition = UMOPcSettings.Transition(held[slot], down);
        held[slot] = down;
        return transition;
    }

    public static void ApplyGraphics()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Settings.MaxFps;
        if (!Settings.LowGraphics) return;
        QualitySettings.antiAliasing = 0;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        // Do not downscale UI atlases or change shader/texture conversion rules.
    }
}
#endif
