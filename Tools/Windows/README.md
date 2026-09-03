# Windows 에셋 진단

## 베타 배포 시점 상태

2026-09-03 전체 아카이브 38,553개 파일 검사·캐시 준비가 `failures=0`으로
완료되었다. 변환이 필요 없는 파일과 기존 정상 캐시도 이 수에 포함된다.
캐시는 Git/Release에 포함하지 않으며 사용자가 원본 데이터로 생성한다.
이 결과는 모든 화면의 실제 표시 검증을 뜻하지 않는다. 아래 final6~8 기록은
개발 이력이며 눈·로딩 배경 등 당시의 문제 목록과 현재 상태를 구분해야 한다.
튜토리얼 안내 수정은 빌드에 반영했지만 전 경로의 실제 확인은 남아 있다.

## 수정 범위

- 게임 콘텐츠: 실행 파일 옆 `Data/android`, `Data/db`.
- 세이브/프로필: 원본과 동일한 `AppData/LocalLow/UtaMacross/UtaMacross` 유지.
- `UMOStandaloneBundleConverter`는 PC에서 메모리로 읽은 UnityFS 번들만 변환한다.
  원본 아카이브 파일과 Android 빌드의 로드 경로는 변경하지 않는다.
- UnityFS의 LZ4 압축 스트림에서 `2018.*` 문자열 뒤 바이트를 직접 바꾸면
  LZ4 반복 참조에 의해 CAB 타입 정보까지 손상된다. `ly/046.xab`에서 재현했다.
  새 변환기는 압축을 풀고 디렉터리로 각 CAB를 찾은 뒤 플랫폼 필드만 바꾼다.
- 지원 컨테이너: UnityFS v6, 무압축/LZ4/LZ4HC. 다른 압축/버전은 명시적으로
  실패 처리한다. 플랫폼 필드 변경만으로 모든 GPU 텍스처/셰이더가 변환되지는 않는다.

## 회귀 테스트

개발용 Python에 `UnityPy`가 필요하다. 일반 게임 실행에는 Python이 필요하지 않다.

```powershell
python Tools/Windows/test_bundle_converter.py
```

테스트는 Windows 빌드의 `Data/android/ly`에서 13개 번들을 읽고 실제 C# 변환기를
PowerShell `Add-Type`으로 실행한다. 변환 전후 CAB의 모든 바이트를 비교하여 플랫폼
필드만 변경되었는지, 모든 오브젝트 데이터가 동일한지 검증한다. 손상된 입력 거부와
이미 변환된 데이터의 무변경 통과도 검사한다. 입력 파일에는 쓰지 않는다.

```powershell
python Tools/Windows/inspect_bundle.py path/to/bundle.xab
```

복호화 후 컨테이너/객체 종류를 출력하는 읽기 전용 검사다.

## 런타임 그래픽 진단

`UMO_Kor.exe -umoGraphicsDiagnostics -logFile <로그 절대경로>`로 실행하면 15초마다
최대 40회 텍스처 압축 형식, GPU 지원 여부, 머티리얼 셰이더 지원 여부를 기록한다.
지원하지 않는 텍스처와 머티리얼은 이름도 기록한다.
진단 기능은 세이브를 수정하지 않는다. 게임 자체의 정상 저장 동작은 별개다.

현재 번들 로드 충돌은 수정 후 해당 지점을 통과했다. 검은 캐릭터/깨진 UI의
렌더링 호환성은 별도 검증 중이며, 화면 검증 전에는 배포 완료로 취급하지 않는다.

## PC 텍스처 캐시 준비

Android ETC 계열 텍스처가 Windows 플레이어에서 지원되지 않는 것을 런타임에서
확인했다. `prepare_texture_cache.py`는 원본을 복호화하여 PC용 RGBA32 텍스처로
변환한 별도 번들을 `Data/WindowsCache/android`에 만든다. 원본 `Data/android`와
ZIP, 세이브에는 쓰지 않는다. 개발용 Python에 UnityPy와 이미지 디코더가 필요하다.

```powershell
# 특정 실행에서 사용한 번들 보충
python Tools/Windows/prepare_texture_cache.py --runtime-log D:/UMO_Kor/Logs/umo-kor-windows-final5-runtime.log

# 메뉴, 모든 배경, 곡 재킷을 재귀적으로 준비
python Tools/Windows/prepare_texture_cache.py --directory Unity/Build/Windows/UMO_Kor/Data/android/ly --directory Unity/Build/Windows/UMO_Kor/Data/android/ct/bg --directory Unity/Build/Windows/UMO_Kor/Data/android/ct/mc

# 전체 콘텐츠 준비: 시간/추가 디스크 공간이 많이 필요함
python Tools/Windows/prepare_texture_cache.py --all
```

각 번들은 다시 열어 원본 디코딩 이미지와 최상위 이미지 픽셀이 동일한지, 이미지
이외의 오브젝트 바이트가 유지되는지 검증한 뒤 저장한다. 하위 밉맵은 재생성된다.
원본 해시가 일치하는 캐시는 재사용한다. 실패는 `last-report.json`에 기록하고
종료 코드 1을 반환한다. 런타임도 복호화한 원본 SHA-256이 일치할 때만 캐시를 쓴다.
데이터 추가/업데이트 후에는 다시 준비해야 한다. 현재 캐시가 없는 번들은 원본으로
돌아가므로 해당 이미지가 깨질 수 있다. 부분 캐시를 전체 변환 완료로 배포하면 안 된다.

캐시를 새로 만든 뒤 이미 실행 중인 게임은 다시 시작해야 한다. 로드된 텍스처를
자동 교체하지 않는다. 일반 사용자가 Python을 직접 실행하지 않도록 하는 설치
자동화/배포 통합은 별도 작업이다.

## PC 곡 시작 음성 초기화

PC의 관리형 CRI 대체 구현에서 `CueInfo.name`을 Editor에서만 채워 실제 실행본에서는
null이 되었고, `BattleEventResultVoice.InitializeCueIndex`의 이름 검사에서 반복
예외가 발생했다. PC에서도 이름과 실제 CueId를 전달하도록 수정했다. Android의
네이티브 구조체 분기는 유지한다.

```powershell
pwsh -NoProfile -File Tools/Windows/test_pc_cue_metadata.ps1
```

실제 메타데이터 공급 코드와 결과 음성 선택 코드를 가짜 ACB 데이터로 실행하여
이름 조회, 인덱스 오류 처리, 0이 아닌 음성 ID 및 초기화를 검증한다. 실제 음악
디코딩/재생까지 확인하는 테스트는 아니며, 게임 실행 검증이 별도로 필요하다.

## 2026-09-03 실행 검증 상태

- `final6` Windows 빌드 성공, 음성 메타데이터 회귀 테스트 통과.
- 메뉴/배경/곡 재킷 디렉터리와 기존 실행 로그에서 수집한 975개 파일 처리 완료.
  `ly/sb`의 4개 `.xab`는 AFS2 음성 컨테이너이므로 이미지 변환에서 제외한다.
  최종 검증 실행의 실패 수는 0이다. 전체 38,553개 콘텐츠의 변환 완료는 아니다.
- 메인 배경/캐릭터/메뉴 표시와 곡 시작 후 스테이지 및 노트 표시를 직접 확인했다.
  `BattleEventResultVoice` 반복 예외는 해당 실행에서 재발하지 않았다.
- 남은 문제: 로딩 배경, 캐릭터 눈, 일부 효과/화면 배치. 지원되지 않는 원본
  셰이더도 런타임에 남아 있으므로 텍스처 변환만으로 완전 해결되지는 않는다.
- 음악이 실제로 들리는지는 사용자 청취 확인이 필요하다. 전곡/전 의상 및
  키보드·게임패드·롱노트 판정은 이번 그래픽 점검으로 검증 완료하지 않았다.

## 추가 그래픽 수정

- PC의 셰이더 교체는 `UMOStandaloneMaterialRepair`를 통해 재질의 원시 큐 값을
  보존한다. `renderQueue`는 셰이더 기본값까지 계산된 값이므로, 이를 복사하면
  원래 기본값 상속(`-1`)이던 재질에 잘못된 그리기 순서를 고정할 수 있다.
  Unity 2018의 비공개 `rawRenderQueue` getter를 읽으며, 해당 API가 없는 버전은
  기본값 비교로 대체한다. Android의 기존 셰이더 교체 경로는 바꾸지 않는다.
- 빌드 시 `ShaderList`에 PC 기본 셰이더를 직접 참조시킨다. 기본 셰이더는
  `AssetDatabase.FindAssets` 검색에 잡히지 않기 때문이다. Unity 2018에 없는
  이름(`Particles/Additive`)으로 등록이 중단되던 중간 빌드도 확인했으므로,
  빌드 도우미에서 목록 생성을 직접 호출해 실패를 빌드 성공으로 오인하지 않게 한다.
- GLES 어셈블리만 있는 메뉴 블러 재질은 Windows에서만 별도 HLSL 셰이더로 교체한다.
  기존 Android 셰이더 파일은 그대로 둔다.
- `UMOStandaloneMaterialTests.PrepareAndTest`로 기본 큐 상속 및 명시적 큐 보존,
  필수 셰이더 목록 생성을 검증한다. 이 실행은 빌드용 TMP 리소스도 준비한다.
- 중간 실행에서 로딩 안내 보드/테두리와 편성 패널/점수 게이지 복구를 확인했다.
  최종 발키리 구간은 추가 빌드 검증 대상이다.
- `gm`, `st`, `vl`의 156개 번들 추가 처리: 변환 144, 캐시 재사용 9, 불필요 3,
  오류 0. 키 매핑은 변경하지 않았다. 로딩 중 입력 시
  `RhythmGameInputPerformer.CheckInputFromKeyTouchInfo`의 null 예외는 별도 미해결 항목이다.

## final8 변경 사항

- 필수 기본 셰이더 6개가 `ShaderList.asset`에 실제 저장된 것을 확인했다.
  `final7`에서 발생했던 잘못된 기본 셰이더 이름 예외 없이 빌드되었다.
- `ct/to`, `ct/im`, `ct/dv`, `ct/tp`와 final7 실행 로그의 파일을 모아
  도움말·우타히메 초상·결과 아이템 텍스처 캐시를 보완한다. 별도 로그:
  `D:/UMO_Kor/Logs/pc-texture-help-results-items.log`.
- PC에서는 롱노트/이동 롱노트의 끝에 flick 표시가 있어도 **끝 타이밍에 키를 놓는
  판정**을 허용한다. 기존 끝 타이밍 평가와 강제 실패 조건은 유지한다. 자동 성공이나
  공중전 진입 조건 완화가 아니다. Android 판정은 그대로다.
- PC의 키 입력은 노트/HUD 초기화가 끝나기 전에는 무시하도록 null 검사를 추가했다.
  키 배치나 별도 설정 UI는 이 수정에서 바꾸지 않았다.
- `python Tools/Windows/test_pc_tail_rules.py`: Android의 EndedTouch 메서드가
  HEAD와 같은지, PC에서 flick 강제 MISS 두 조건만 제거되는지 검사하는 소스 수준
  테스트다. 실제 입력 타이밍 및 게임패드 테스트를 대신하지 않는다.

## 홈 튜토리얼 정지 진단

- `-umoGraphicsDiagnostics` 실행 시 `[UMO PC menu wait]`에는 홈/튜토리얼/
  화면 전환 코루틴의 숫자·불리언 상태만, `[UMO PC menu animation]`에는
  진입/퇴장 애니메이션의 위치·상태를 기록한다. 저장 데이터나 진행 플래그,
  입력 잠금을 변경하지 않는 진단 기능이다.
- 2026-09-03의 CHECK 아래 배너 밀림 및 홈 튜토리얼 정지는 기존 로그에
  예외가 없어 아직 원인 확정 전이다. 진단 빌드 성공과 실제 문제 해결은
  별개이며 재현 로그로 확인해야 한다.
- 도움말/아이템/우타히메/팁 캐시 확장 작업은 총 7,838개 파일 검사·처리,
  오류 0으로 완료되었다. 모든 파일을 변환했다는 뜻은 아니며 기존 캐시와
  변환 불필요 파일도 포함한다.

### 재현 후 확인 사항

- 홈 정지는 `Co_BeginnerMissionLiveClear`의 미션 버튼 클릭 대기였다. 사용자가
  실제 버튼을 찾아 누르자 진행되었다. 전체 입력 정지로 단정하지 않는다.
- 해당 경로가 `PreLoadResource(..., false)`로 손가락/강조 표시를 생략하므로,
  Windows에서는 기본 튜토리얼 사전 로딩 때 추가 안내 레이아웃도 준비한다.
  Android 분기와 세이브/진행 조건, 버튼 제한 규칙은 그대로 유지한다.
  `test_pc_tutorial_guidance.py`는 이 범위만 검사하는 소스 회귀 테스트다.
- 공지 배너는 위치 밀림이 아니라 텍스처 누락/손상으로 사용자 확인됨.
  배너 위치와 스크롤 코드는 수정하지 않았다.
- 전체 38,553개 아카이브 파일 캐시 확장 실행:
  `python Tools/Windows/prepare_texture_cache.py --all --workers 4`.
  각 작업은 서로 다른 경로만 쓰며 변환 후 픽셀 및 비텍스처 객체를 검증한다.
  완료 여부와 실패 수는 `D:/UMO_Kor/Logs/pc-texture-full-archive-parallel.log`의
  마지막 `Done:` 및 `Data/WindowsCache/last-report.json`으로 확인한다.
  실행 중에는 완료된 것으로 간주하지 않는다.
