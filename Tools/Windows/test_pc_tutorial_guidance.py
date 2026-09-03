"""Source regression only: PC preloads guidance; Android loading is unchanged."""
import subprocess
from test_pc_tail_rules import ROOT, BASELINE, preprocess, tokens

relative = 'Unity/Assets/UMAssets/Scripts/XeApp/Game/Tutorial/BasicTutorialManager.cs'
source = (ROOT / relative).read_text(encoding='utf-8-sig')
baseline = subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT).decode('utf-8-sig')

def method(text):
    return text.split('private IEnumerator PreLoadLayoutCoroutine(', 1)[1].split(
        'private IEnumerator LoadBaseLayoutCoroutine()', 1)[0]

android = preprocess(method(source), {'UNITY_ANDROID'})
assert tokens(android) == tokens(preprocess(method(baseline), {'UNITY_ANDROID'}))
pc = preprocess(method(source), {'UNITY_STANDALONE_WIN'})
assert pc.index('isAppendLayout = true;') < pc.index('LoadBaseLayoutCoroutine()')
assert 'LoadAppendLayoutCoroutine()' in pc
assert tokens(pc.replace('isAppendLayout = true;', '', 1)) == tokens(android)
print('PASS: PC preloads guidance; Android coroutine unchanged; no progress/input bypass added.')
