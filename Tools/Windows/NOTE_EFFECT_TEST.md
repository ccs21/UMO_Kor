# 첫 번째 내부 레인 이펙트 검증

대상은 내부 track 0(4레인 맨 왼쪽, 6레인 왼쪽 두 번째)입니다.

## 재현 결과 (2026-09-05)

- 게임 로그에서는 입력, 애니메이션 상태, Renderer 활성화가 다른 레인과 같았습니다. `Renderer.isVisible`은 실제 픽셀 표시를 보장하지 않습니다.
- 설치된 `Data/WindowsCache/android/gm/if/un.xab`의 `ui_rhythm_obj.prefab`을 이용해 여섯 개를 순서대로 복제했습니다.
- 복제 **후** 셰이더를 교체하면 첫 번째 `fx_rhythm_flash02`의 애니메이션 MaterialPropertyBlock `_Alpha`가 0, 이후 복제본은 1이었습니다. Push 및 Long_Loop 양쪽에서 확인했습니다.
- 원본 프리팹의 공유 재질을 PC 셰이더로 바꾼 **후** 복제하면 여섯 개 모두 `_Alpha=1` 검사를 통과합니다.
- `TouchPrefabInstance.Instantiate`의 Windows 독립 실행 분기에서 이 순서를 적용했습니다. Android/Editor의 게임 동작은 그대로입니다.

## 독립 검사

Unity 2018.4.36f1에서 `-batchmode -quit -buildTarget Win64 -projectPath <Unity 폴더>`와 아래 메서드를 사용합니다. 게임/세이브를 로드하지 않습니다.

- `-executeMethod UMONoteEffectProbe.Run`: 이전 순서의 첫 번째 복제본 alpha=0 재현 검사.
- `-executeMethod UMONoteEffectProbe.RunFixed`: 수정 순서의 여섯 복제본 alpha=1 검사.

검사는 기존 PC 설치의 위 캐시 파일을 읽으며, 해당 외부 셰이더 번들을 별도로 로드하지 않으므로 프로젝트의 `rhythmuivertexcolor.shader`를 명시적으로 사용합니다. 실제 게임 전체의 가림/카메라 상태까지 검증하는 테스트는 아닙니다.

최종 확인은 PC 게임에서 4레인 첫 번째와 6레인 두 번째의 일반 노트/롱노트 이펙트를 직접 비교합니다. 임시 `UMOPcNoteEffectTrace` 로그에는 `animatedAlpha`와 `materialAlpha`도 기록됩니다.
