using System;
using System.IO;
using System.Collections.Generic;
using XeApp.Game.RhythmGame;
using XeApp.Game.Common;

// Compile the real judge source against minimal non-engine declarations.
namespace UnityEngine { public class SerializeField : Attribute {} }
namespace XeApp.Game.Common { public static class RhythmGameConsts { public enum NoteResult { Miss, Bad, Good, Great, Perfect, Num, None, Exempt } } }
public static class UMOPcRuntime { public static UMOPcSettings Settings = new UMOPcSettings(); }
public static class TestPcSettings
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static void Main()
    {
        var config = new UMOPcSettings();
        Check(config.Validate() == null, "defaults");
        Check(string.Join("", config.Keys4) == "SDKL" && string.Join("", config.Keys6) == "SDFJKL", "bindings");
        Check(UMOPcSettings.VisibleLane(0, 4) == -1 && UMOPcSettings.VisibleLane(1, 4) == 0 && UMOPcSettings.VisibleLane(4, 4) == 3 && UMOPcSettings.VisibleLane(5, 4) == -1, "four-lane slots");
        for (int i = 0; i < 6; i++) Check(UMOPcSettings.VisibleLane(i, 6) == i, "six-lane slots");
        Check(UMOPcSettings.Transition(false, true) == 0 && UMOPcSettings.Transition(true, true) == 2 && UMOPcSettings.Transition(true, false) == 1 && UMOPcSettings.Transition(false, false) == -1, "edges");
        // Releasing a keyboard key while its pad button remains held has no up edge.
        Check(UMOPcSettings.Transition(true, false || true) == 2, "mixed device hold");
        config.Keys4[1] = "S"; Check(config.Validate() != null, "duplicate rejected");
        config = new UMOPcSettings(); config.SkillKey = "D"; Check(config.Validate() != null, "skill conflict rejected");
        config = new UMOPcSettings(); config.AssistMs = 1000; Check(config.Validate() != null, "invalid assist rejected");
        config = new UMOPcSettings(); config.Pad4[0] = "NotAButton"; Check(config.Validate() != null, "bad pad rejected");
        config = new UMOPcSettings();
        string folder = Path.Combine(Path.GetTempPath(), "umo-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, UMOPcSettings.FileName);
        config.Save(file); config.AssistMs = 0; config.Height = 800; config.Save(file);
        Check(UMOPcSettings.Load(file).Height == 800 && UMOPcSettings.Load(file).AssistMs == 0 && UMOPcSettings.Load(file + ".bak").AssistMs == 15, "roundtrip and backup");
        // No recursive deletion: remove only these generated test files.
        File.Delete(file); File.Delete(file + ".bak"); Directory.Delete(folder);
        var judge = new RNoteResultJudge(new List<int> { -100, -70, -40, -20 }, new List<int> { 100, 70, 40, 20 });
        foreach (int assist in new[] { 0, 15, 30 })
        {
            UMOPcRuntime.Settings.AssistMs = assist;
#if UNITY_STANDALONE_WIN
            int extra = assist;
#else
            int extra = 0;
#endif
            Check(judge.CalcEvaluation(-20 - extra, 0) == RhythmGameConsts.NoteResult.Perfect, "early boundary");
            Check(judge.CalcEvaluation(20 + extra, 0) == RhythmGameConsts.NoteResult.Perfect, "late boundary");
            Check(judge.CalcEvaluation(21 + extra, 0) == RhythmGameConsts.NoteResult.Great, "grade order");
            Check(judge.CalcEvaluation(100 + extra, 0) == RhythmGameConsts.NoteResult.Bad, "late expiry boundary");
            Check(judge.CalcEvaluation(101 + extra, 0) == RhythmGameConsts.NoteResult.Miss, "expired");
            Check(judge.CalcEvaluation(-101 - extra, 0) == RhythmGameConsts.NoteResult.Exempt, "early exempt");
            Check(judge.CalcEvaluation(25 + extra, 5) == RhythmGameConsts.NoteResult.Perfect, "skill bonus preserved");
        }
        Console.WriteLine("PASS: settings, validation, backup, key slots, input edges, timing/expiry boundaries");
    }
}
