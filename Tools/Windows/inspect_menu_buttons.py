"""Read-only check of menu prefab traversal order versus semantic button IDs."""
from pathlib import Path
import UnityPy
from inspect_bundle import decrypt_bundle

root = Path(__file__).resolve().parents[2]
env = UnityPy.load(decrypt_bundle(
    root / 'Unity/Build/Windows/UMO_Kor/Data/android/ly/005.xab',
    root / 'Data/RequestMaster.json'))
trees = {o.path_id: o.read_typetree() for o in env.objects
         if o.type.name in ('GameObject', 'Transform', 'RectTransform', 'MonoBehaviour', 'MonoScript')}
menu = next(t for t in trees.values() if 'mMenuButtons' in t)
menu_id = menu['m_GameObject']['m_PathID']
menu_transform = next(k for k, t in trees.items()
                      if t.get('m_GameObject', {}).get('m_PathID') == menu_id and 'm_Children' in t)

def walk(key):
    transform = trees[key]
    obj = trees[transform['m_GameObject']['m_PathID']]
    for component in obj['m_Component']:
        data = trees.get(component['component']['m_PathID'], {})
        script = trees.get(data.get('m_Script', {}).get('m_PathID'), {})
        if script.get('m_ClassName') in ('LayoutUGUIHitOnly', 'MenuButtonAnim'):
            print(obj['m_Name'], script['m_ClassName'], data.get('m_buttonType', ''))
    for child in transform['m_Children']:
        walk(child['m_PathID'])

walk(menu_transform)
