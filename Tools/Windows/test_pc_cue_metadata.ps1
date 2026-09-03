$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# Compile the actual PC metadata provider and result-voice consumer without
# Unity or native CRI libraries. A fake ACB contains deliberately nonzero IDs.
$testStubs = @'
using System;
using System.IO;
using System.Collections.Generic;
using CriWare;
using UnityEngine;
using VGMToolbox.format;
namespace UnityEngine {
    public class AudioClip {}
    public static class Random { public static int Range(int a, int b) { return a; } }
}
public static class TodoLogger {
    public const int CriAtomExLib = 0;
    public static void LogError(int category, string message) {}
}
namespace VGMToolbox.format {
    public class CriAcbCueRecord {
        public long LengthMilli = 1234;
        public uint CueId;
        public string OriginalCueName;
    }
    public class CriAcbFile {
        public CriAcbCueRecord[] CueList = {
            new CriAcbCueRecord { CueId = 17, OriginalCueName = "g_bttresult_001_a" },
            new CriAcbCueRecord { CueId = 42, OriginalCueName = "g_bttresult_002_a" }
        };
        public CriAcbFile(FileStream f, long offset, bool flag, string external) {}
        public CriAcbCueRecord GetCueRecord(string name) { return Array.Find(CueList, x => x.OriginalCueName == name); }
        public CriAcbCueRecord GetCueRecordByIdx(int index) { return index >= 0 && index < CueList.Length ? CueList[index] : null; }
    }
}
namespace ExternLib {
    public static partial class LibCriWare {
        static UnityEngine.AudioClip GetClip(VGMToolbox.format.CriAcbFile file, string name, int id) { return null; }
    }
}
namespace CriWare {
    public class CriFsBinder {}
    public class CriAtomSource { public string cueSheet = "test"; }
    public static class CriAtomEx { public struct CueInfo { public int id; public string name; public long length; } }
    public class CriAtomCueSheet { public CriAtomExAcb acb; }
    public static class CriAtom {
        public static CriAtomCueSheet sheet;
        public static CriAtomCueSheet GetCueSheet(string name) { return sheet; }
    }
    public class CriAtomExAcb {
        public IntPtr handle;
        public CriAtomEx.CueInfo[] GetCueInfoList() {
            var result = new CriAtomEx.CueInfo[ExternLib.LibCriWare.criAtomExAcb_GetNumCues(handle)];
            for(int i=0; i<result.Length; i++) ExternLib.LibCriWare.criAtomExAcb_GetCueInfoByIndex(handle,i,out result[i]);
            return result;
        }
    }
}
public static class CueMetadataTest {
    public static void Run(string existingFile) {
        var handle = ExternLib.LibCriWare.criAtomExAcb_LoadAcbFile(null,existingFile,null,"",IntPtr.Zero,0);
        CriWare.CriAtomEx.CueInfo info;
        if(!ExternLib.LibCriWare.criAtomExAcb_GetCueInfoByName(handle,"g_bttresult_002_a",out info) || info.id != 42 || info.name != "g_bttresult_002_a" || info.length != 1234) throw new Exception("By-name metadata failed");
        if(ExternLib.LibCriWare.criAtomExAcb_GetCueInfoByIndex(handle,99,out info)) throw new Exception("Invalid index accepted");
        CriWare.CriAtom.sheet = new CriWare.CriAtomCueSheet { acb = new CriWare.CriAtomExAcb { handle=handle } };
        var voice = new XeApp.Game.RhythmGame.BattleEventResultVoice(new CriWare.CriAtomSource());
        if(voice.GetCueRandomId(XeApp.Game.RhythmGame.BattleEventResultVoice.ResultVoiceIndex.Valkyeri) != 17 || voice.GetCueRandomId(XeApp.Game.RhythmGame.BattleEventResultVoice.ResultVoiceIndex.DivaMode) != 42) throw new Exception("Result voice names/IDs failed");
        ExternLib.LibCriWare.criAtomExAcb_Release(handle);
    }
}
'@
$provider = Get-Content -Raw -LiteralPath "$repoRoot/Unity/Assets/UMAssets/Scripts/_LibC/CriWare/CriAtomExAcb.cs"
$consumer = Get-Content -Raw -LiteralPath "$repoRoot/Unity/Assets/UMAssets/Scripts/XeApp/Game/RhythmGame/BattleEventResultVoice.cs"
Add-Type -TypeDefinition ($testStubs + "`n" + ($provider -replace '(?m)^using [^;]+;', '') + "`n" + ($consumer -replace '(?m)^using [^;]+;', ''))
[CueMetadataTest]::Run($PSCommandPath)
Write-Output 'PASS: PC cue names, nonzero IDs, invalid index, and result-voice initialization.'
