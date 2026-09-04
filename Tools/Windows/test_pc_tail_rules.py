"""Source-level regression: desktop flick-tail exception, unchanged Android method.

This verifies build branches, not real-time keyboard timing or gameplay.
"""
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RELATIVE = "Unity/Assets/UMAssets/Scripts/XeApp/Game/RhythmGame/RNoteOwner.cs"
BASELINE = "8a0ea96d1681e6b3d96b4e97e6f1153dbdbde6df"


def preprocess(text, defines):
    output, stack = [], [True]
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("#if "):
            expression = stripped[4:].split("//")[0].strip()
            assert re.fullmatch(r"[A-Za-z0-9_!&|() \t]+", expression), expression
            expression = re.sub(r"\b[A-Za-z_]\w*\b", lambda m: str(m[0] in defines), expression)
            expression = expression.replace("&&", " and ").replace("||", " or ").replace("!", " not ")
            stack.append(stack[-1] and bool(eval(expression.strip(), {"__builtins__": {}})))
        elif stripped == "#endif":
            stack.pop()
        elif stripped == "#else":
            stack[-1] = stack[-2] and not stack[-1]
        elif stripped.startswith("#"):
            raise AssertionError("Unsupported directive: " + stripped)
        elif stack[-1]:
            output.append(line)
    assert len(stack) == 1
    return "\n".join(output)


def ended_touch(text):
    return text.split("public void EndedTouch(", 1)[1].split("public void ReleaseLine(", 1)[0]


def tokens(text):
    return re.sub(r"\s+", "", re.sub(r"//[^\n]*", "", text))


def main():
    source = ended_touch((ROOT / RELATIVE).read_text(encoding="utf-8-sig"))
    baseline = ended_touch(subprocess.check_output(["git", "show", BASELINE + ":" + RELATIVE], cwd=ROOT).decode("utf-8-sig"))
    android = preprocess(source, {"UNITY_ANDROID"})
    assert tokens(android) == tokens(preprocess(baseline, {"UNITY_ANDROID"})), "Android tail method changed"
    pc = preprocess(source, {"UNITY_STANDALONE", "UNITY_STANDALONE_WIN"})
    for method in (android, pc):
        assert "CalcEvaluation" in method and "if(forceMiss)" in tokens(method)
    # Only the unconditional flick-to-Miss rules are removed on desktop.
    pattern = r"if\s*\([^\n]*lastRNoteObject\.rNote\.noteInfo\.flick[^\n]*\)\s*res = RhythmGameConsts.NoteResult.Miss;"
    assert len(re.findall(pattern, android)) == 2
    assert len(re.findall(pattern, pc)) == 0
    assert "if(true)" in tokens(pc), "PC slide tail must allow starting lane release"
    assert "lastRNoteObject.IsJudged()" in pc
    full = (ROOT / RELATIVE).read_text(encoding="utf-8-sig")
    old = subprocess.check_output(["git", "show", BASELINE + ":" + RELATIVE], cwd=ROOT).decode("utf-8-sig")
    neutral = lambda s: s.split("public void NeutralTouch(", 1)[1].split("public void CheckInputCallback(", 1)[0]
    assert tokens(preprocess(neutral(full), {"UNITY_ANDROID"})) == tokens(preprocess(neutral(old), {"UNITY_ANDROID"}))
    print("PASS: Android tail/hold methods unchanged; PC retains timing/forceMiss and allows cross-lane holds.")


if __name__ == "__main__":
    main()
