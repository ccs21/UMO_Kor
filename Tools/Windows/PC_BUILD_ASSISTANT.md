# 우타마크로스 PC 빌드 도우미

`UMO_PC_Build_Assistant.exe`는 Git 클론부터 Unity 빌드, 원본 데이터 배치, Windows 텍스처 변환과 키 설정 실행까지 한 화면에서 처리하는 Windows용 도구입니다. PowerShell이나 명령 프롬프트에 명령을 입력할 필요가 없습니다.

## 시작하기

1. Releases의 `UMO_Kor_For_windows_20260910.zip`을 쓰기 가능한 새 폴더에 풉니다. 바탕 화면이나 여유 공간이 충분한 별도 드라이브를 권장합니다. 동봉된 `우타마크로스 오프라인 PC 빌드 도우미 사용방법.pdf`도 함께 참고하세요.
2. `UMO_PC_Build_Assistant.exe`와 `UMO_PC_Build_Assistant.exe.config`를 서로 같은 폴더에 둡니다.
3. EXE를 실행합니다. 도우미가 실행된 폴더가 기본 작업 폴더로 선택됩니다.
4. 화면의 1~5페이지를 순서대로 완료합니다. 현재 페이지 검사가 통과하면 오른쪽 아래 `다음` 버튼이 활성화됩니다.

Python 패키지 설치, Git 소스 준비·Unity 빌드, 텍스처 변환·검증 페이지에는 진행률 막대와 현재 단계가 표시됩니다. 내부 프로그램이 정확한 퍼센트를 제공하지 않는 구간은 막대가 계속 움직이며 작업 중임을 알리고, 텍스처 변환 중에는 처리된 파일 수와 전체 파일 수를 실제 퍼센트로 표시합니다.

관리자 권한은 기본적으로 필요하지 않습니다. `Program Files`처럼 일반 사용자가 파일을 쓸 수 없는 폴더에서는 작업하지 마세요. 원본 데이터와 변환 캐시를 함께 보관하므로 수십 GB 이상의 빈 공간이 필요합니다.

## Python 설치 시 주의사항

도우미의 `Python 설치 파일 받기` 버튼은 이 프로젝트에서 검증한 **Python 3.12.10 64비트 일반 설치 프로그램** 다운로드를 바로 시작합니다. 영어 다운로드 페이지에서 파일 종류를 고를 필요가 없습니다.

설치 프로그램 첫 화면에서는 다음을 확인합니다.

- **Add python.exe to PATH**를 반드시 체크합니다.
- **Install launcher for all users (recommended)** 또는 `py launcher`가 표시되면 체크 상태를 유지합니다.
- `Install Now`로 설치해도 됩니다. `Customize installation`을 선택했다면 **pip**, **py launcher**와 기본 권장 구성 요소를 해제하지 마세요. Python test suite는 필수가 아닙니다.
- `venv`는 Python 표준 구성 요소입니다. 설치 용량을 줄이려고 표준 라이브러리나 `pip`를 제외하면 도우미가 전용 환경을 만들 수 없습니다.

설치가 끝난 뒤 `Disable path length limit` 버튼이 보이면 눌러도 됩니다. 긴 Unity 파일 경로에서 발생할 수 있는 제한을 줄여 줍니다. 회사·학교 PC라서 관리자 승인이 필요한 경우에는 건너뛰어도 되지만, 작업 폴더를 `D:\UMO_PC`처럼 짧은 경로에 두세요.

다음 설치 방식은 피하세요.

- Microsoft Store의 실행 별칭만 있고 실제 Python이 설치되지 않은 상태
- 압축만 풀어 쓰는 embeddable package
- 32비트 Python
- Python 2 또는 Python 3.9 이하

설치 후 도우미로 돌아와 `설치 상태 새로고침`을 누릅니다. 계속 Python이 없다고 나오면 도우미를 한 번 종료했다가 다시 실행하세요. 그래도 감지되지 않으면 Windows의 **앱 실행 별칭**에서 `python.exe`와 `python3.exe` Store 별칭을 끄고 공식 설치 프로그램을 다시 실행합니다.

## Unity 설치 시 주의사항

도우미의 `Unity 설치 파일 받기` 버튼은 정확한 `Unity Editor 2018.4.36f1` Windows 64비트 설치 프로그램 다운로드를 바로 시작합니다. 비슷한 이름의 다른 2018.4 버전이나 최신 Unity로 대체하면 안 됩니다. Unity Hub를 이미 사용한다면 Hub에 같은 버전이 설치되어 있어도 됩니다.

모듈 선택 화면에서는 다음을 확인합니다.

- **Windows Build Support (Mono)**가 별도 항목으로 보이면 반드시 선택합니다. 직접 받은 Windows용 Editor 설치 프로그램에 기본 포함되어 별도 항목이 보이지 않는 경우에는 그대로 진행합니다.
- Microsoft Visual Studio는 코드 수정에 편리하지만 도우미로 빌드만 할 때는 필수 항목이 아닙니다.
- PC판만 만들 때 Android Build Support, Android SDK/JDK/NDK는 필요하지 않습니다.
- 한국어 언어 팩은 선택 사항이며 게임의 한국어 번역 포함 여부와 관계없습니다.

설치가 끝나면 2018.4.36f1을 **한 번 직접 실행**하고 Unity 계정 로그인과 라이선스 활성화를 마친 뒤 에디터를 닫으세요. Unity Hub로 라이선스를 관리한다면 Hub에서 직접 설치한 Editor 위치를 추가할 수 있습니다. 라이선스 안내창이나 로그인창이 남아 있으면 자동 빌드가 대기 상태에서 멈출 수 있습니다.

주의할 점:

- 프로젝트를 열 때 “새 Unity 버전으로 업그레이드”하라는 안내가 나오면 진행하지 마세요.
- 도우미가 빌드하는 동안 Unity Editor에서 같은 프로젝트를 동시에 열지 마세요.
- Unity 설치 경로를 바꿨거나 도우미가 찾지 못하면 1페이지의 Unity `찾아보기`를 눌러 `2018.4.36f1\Editor\Unity.exe`를 직접 선택합니다.
- Unity Hub의 설치가 끝나기 전에 도우미의 새로고침을 누르면 미설치로 표시됩니다. Hub에서 설치 완료를 확인한 뒤 다시 검사하세요.

## Git과 .NET Framework

Git for Windows는 설치 프로그램의 기본 선택을 그대로 사용해도 됩니다. 설치 직후 감지되지 않으면 도우미를 재실행하세요. `.NET Framework 4.8 개발자 팩`은 C# 컴파일러가 없는 PC에서만 설치하면 됩니다. 설치 후 Windows 재시작을 요구하면 재시작한 다음 도우미를 다시 실행합니다.

## ZIP 파일 넣기

4페이지에서 `ZIP 파일 넣을 폴더 열기`를 누르면 정확한 보관 폴더가 파일 탐색기로 열립니다. 다음 두 파일을 이름을 바꾸지 않고 그 폴더에 복사합니다.

- `UtaMacrossDataArchive.zip`
- `UtaMacrossDataArchivePCPatch.zip`

압축을 직접 풀거나 ZIP 내부 구조를 바꾸지 마세요. 복사가 완전히 끝난 뒤 `새로고침 및 자동 배치`를 누릅니다. 원본 ZIP은 삭제하거나 수정하지 않으며, 재실행 시 이미 정상 배치된 같은 크기의 파일은 다시 쓰지 않습니다.

공식 로그인 보너스 DLC는 따로 받을 필요가 없습니다. 이 단계에서 도우미가 원본 UMO 서버에서 자동으로 다운로드하고 SHA-256을 검증한 뒤 설치·활성화합니다. DLC의 Android용 배경 번들은 마지막 단계에서 PC용으로 함께 변환됩니다.

## 완성된 게임 찾기

마지막 검증을 통과한 뒤 `게임 폴더 열기 및 키 설정`을 누르면 다음 두 동작을 수행하고 도우미가 종료됩니다.

- 완성된 `UMO_Kor.exe`가 있는 폴더를 파일 탐색기로 엽니다.
- 같은 폴더의 `UMO_PC_Settings.exe`를 실행합니다.

게임을 다른 곳으로 옮길 때는 `UMO_Kor.exe` 하나만 복사하지 말고 열린 게임 폴더 전체를 함께 이동해야 합니다.

PC판의 프로필과 세이브는 게임 실행 파일 옆의 `UserData` 폴더에 저장됩니다. 기존 `%USERPROFILE%\AppData\LocalLow\UtaMacross\UtaMacross` 세이브가 있으면 게임 첫 실행 때 자동으로 복사합니다. 이미 `UserData`에 있는 파일은 덮어쓰지 않습니다. 게임을 옮기거나 업데이트할 때는 `UserData`를 반드시 함께 보존하세요.

## 도우미 자체를 소스에서 만들기

저장소를 이미 받은 개발자는 저장소 최상위에서 다음 스크립트를 실행할 수 있습니다.

```powershell
.\Tools\Windows\Build-PcBuildAssistant.ps1
```

결과는 기본적으로 `BuildTools/UMO_PC_Build_Assistant.exe`와 그 옆의 `.config` 파일입니다. 일반 사용자는 이 명령을 실행할 필요가 없습니다.

배포용 ZIP까지 만들 때는 다음 스크립트를 사용합니다.

```powershell
.\Tools\Windows\Publish-PcBuildAssistant.ps1
```

결과는 `outputs/PcBuildAssistant/UMO_PC_Build_Assistant.zip`입니다. EXE, 고배율 설정용 `.config`, 한글 사용법이 함께 들어갑니다.
