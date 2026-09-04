"""Compile the actual input callback with fake per-track HUDs to test ordering."""
import os
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "Unity/Assets/UMAssets/Scripts/XeApp/Game/RhythmGame/RhythmGamePlayer.cs").read_text(encoding="utf-8-sig")
method = source[source.index("public void BeganTouchEventCallback("):source.index("public void EndedTouchEventCallback(")]
method = method[:method.rfind("}") + 1]
support = r'''
using System;
class Hud {
 public bool isReadyHUD=true;
 public string[] State=new string[6];
 public bool IsInputAccept(){return true;}
 public void ShowTouchEffect(int line,int finger){State[line]="push";}
 public void ShowLongNotesTouchEffect(int line){State[line]="long";}
}
class Ui {public Hud Hud=new Hud();}
class Notes {
 public Hud Hud;
 public int Hits;
 public bool BeganTouch(int line,int finger){Hits++; Hud.ShowLongNotesTouchEffect(line);return true;}
 public bool IsLongBeganTouched(int line){return true;}
 public bool IsSlideBeganTouched(int finger){return false;}
}
class Performer {public void BeginTouchSave(int line){}}
class Test {
 Ui uiController=new Ui(); Notes rNoteOwner=new Notes(); Performer gamePerformer=new Performer();
 bool isVisiblePauseWindow=false;
 enum NoteSEType{Exempt}
 void PlayNotesSE(NoteSEType type){}
 METHOD
 static void Main(){
  var t=new Test(); t.rNoteOwner.Hud=t.uiController.Hud;
  t.BeganTouchEventCallback(0,0);t.BeganTouchEventCallback(3,1);
  if(t.rNoteOwner.Hits!=2)throw new Exception("Both hits must be processed");
#if UNITY_STANDALONE_WIN
  string expected="long";
#else
  string expected="push"; // unchanged original callback ordering
#endif
  if(t.uiController.Hud.State[0]!=expected || t.uiController.Hud.State[3]!=expected)throw new Exception("Effect overwritten");
  Console.WriteLine("PASS: two simultaneous track effects, platform callback order");
 }
}
'''
output = root / "Unity/Build/PcSettingsTests"
output.mkdir(parents=True, exist_ok=True)
program = output / "HoldEffects.cs"
program.write_text(support.replace("METHOD", method), encoding="utf-8-sig")
compiler = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
for define in ("UNITY_STANDALONE_WIN", "UNITY_ANDROID"):
    executable = output / ("Hold-" + define + ".exe")
    subprocess.run([str(compiler), "/nologo", "/codepage:65001", "/define:" + define, "/out:" + str(executable), str(program)], check=True)
    subprocess.run([str(executable)], check=True)
