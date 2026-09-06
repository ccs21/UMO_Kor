# 우타마크로스 — UMO 한국어판

UMO의 비공식 한국어 포크입니다. 한국어 텍스트를 앱에 내장하여 별도 언어 DLC 없이 한국어로 시작합니다.

**현재 베타입니다. 이미지에 그려진 일본어는 아직 남아 있고, Windows판의 일부 이미지·셰이더·튜토리얼 표시는 검증 중입니다. 100% 한글화 또는 완성판이 아닙니다.**

[한국어판 베타 다운로드](https://github.com/ccs21/UMO_Kor/releases) · [문제 제보](https://github.com/ccs21/UMO_Kor/issues)

## 원본 포크 정보

- 원본: [Xele02/UMO](https://github.com/Xele02/UMO), Xele02 및 기여자.
- 한국어 포크: [ccs21/UMO_Kor](https://github.com/ccs21/UMO_Kor).
- 개발 기준: UMO 1.1.16. 원본 최신 버전과 별도로 관리합니다.
- 원본 문서: [UMO Knowledge Base](https://umo.xele.org/).

원본 UMO 개발자나 마크로스 권리자의 공식 한국어 서비스가 아닙니다. 한국어판 문제는 이 저장소에 제보해 주세요.

## 프로젝트 개요

서비스가 종료된 **우타마크로스 스마트폰 De컬쳐**를 오프라인으로 실행하는 UMO를 한국어로 즐기기 위한 프로젝트입니다. 일본어만 번역하며 영어 곡명 등은 유지합니다.

이번 배포 범위는 Android 한국어 beta.7과 Windows 개발 소스·빌드 도우미입니다. Android beta.7에는 수정4까지의 번역, 스테이터스 화면의 한글 폰트 누락 수정 및 의상강화 항목명 축약이 반영됩니다. PC도 같은 공용 번역을 사용합니다. Windows 독립 실행 빌드, PC용 텍스처 캐시, 그래픽 진단, 키·게임패드 설정 도구, PC 전용 판정 보조·저부하 설정·이동 롱노트 보완이 포함됩니다. 전곡·전 의상·게임패드 검증은 완료되지 않았습니다. [PC 설정 및 검수용 자원 충전 안내](Tools/Windows/PC_SETTINGS.md)를 참고하세요. 검수용 자원 충전은 게임을 종료한 뒤 선택한 PC 원본 세이브에 직접 적용하며, 매번 먼저 백업하고 사용자 확인을 받습니다. 프로필 전환은 필요 없습니다.

Android 추가 데이터 설치용 **한국어 PC 서버 도우미**가 포함됩니다. ZIP 두 개를 지정 폴더에 넣으면 압축 해제, 파일 배치, 네트워크 주소 안내와 서버 시작을 자동으로 처리합니다. 게임 데이터와 PC 패치 자체는 저장소 및 Releases에 포함하지 않습니다. 추가 DLC는 [원본 README의 New content](https://github.com/Xele02/UMO#new-content)를 참고해 별도로 설치합니다.

## Android 설치 방법

### 기존 일본어판·개발용 테스트판 사용자 주의

**기존 일본어판과 한국어 배포판의 세이브는 호환되지 않는 것으로 안내하며, 자동 승계·가져오기를 지원하지 않습니다.** 내부 저장 형식 전체가 다르다는 뜻이 아니라 별도 앱으로 설치되고 이전을 검증·지원하지 않는다는 뜻입니다. 한국어판에서는 새로 시작해 주세요.

- 원본: `com.UtaMacrossOffline.UtaMacrossOffline`
- 이전 개발용 테스트판: `com.ccs21.UMOKorTest`
- 이번 베타부터 한국어 배포판: **`com.ccs21.umokor`**

원본 앱을 지울 필요가 없습니다. 기존 기록 보존을 위해 원본 앱 삭제·저장공간 초기화를 하지 마세요. 이전 README의 원본 세이브 호환 목표는 이번 베타의 지원 범위가 아닙니다.

### APK 설치

1. [Releases](https://github.com/ccs21/UMO_Kor/releases)에서 **Pre-release(베타)**의 `UMO_Kor_For_Android_20260907.zip`을 받아 PC에서 압축을 풉니다. Source code ZIP은 설치 파일이 아닙니다.
2. 압축 안의 `다운 받아야 하는 파일은 이 두개 입니다.png`를 보고 원본 데이터 ZIP 두 개를 준비합니다. 함께 들어 있는 APK를 휴대폰으로 옮겨 설치합니다.
3. 압축 안의 `UMO_PC_Server.exe`를 아래 안내대로 실행해 추가 데이터를 설치합니다. APK에는 전체 게임 데이터가 포함되지 않습니다.

이미 한국어판 beta.1~beta.6을 사용 중인 경우에는 합본 ZIP을 다시 받을 필요 없이 릴리스의 `UMO_Kor-1.1.16-ko-beta.7.apk`만 받아 기존 앱을 삭제하지 않고 덮어 설치하세요. APK 단독 파일은 신규 설치자의 데이터 서버를 포함하지 않습니다.

### 한국어 PC 서버 도우미로 데이터 설치

아카이브와 PC 패치는 [원본 Android 설치 안내](https://umo.xele.org/getting-started/installation/install-android/)의 **Game datas** 링크에서 받습니다. `UtaMacrossDataArchive.zip`, `UtaMacrossDataArchivePCPatch.zip` 및 토렌트가 안내되어 있습니다. **이 저장소와 한국어판 Releases에는 아카이브·PC 패치·변환 캐시를 재업로드하지 않습니다.**

PC를 서버로 사용하는 순서입니다. Unity 설치나 경로 입력은 필요하지 않습니다.

1. 한국어판 Releases에서 `UMO_Kor_For_Android_20260907.zip`을 받아 쓰기 가능한 새 폴더에 풉니다.
2. `UMO_PC_Server.exe`를 실행하고 `ZIP 파일 넣을 폴더 열기`를 누릅니다.
3. 열린 `Archives` 폴더에 `UtaMacrossDataArchive.zip`과 `UtaMacrossDataArchivePCPatch.zip`을 **압축을 풀지 않고 그대로** 넣습니다.
4. `새로고침`을 누른 뒤 `자동 설치 및 서버 시작`을 누릅니다. 프로그램 옆 `ServerData`에 다음 구조가 자동으로 만들어집니다.

```text
D:/원하는 폴더/UMO_PC_Server/
  UMO_PC_Server.exe
  Archives/
    UtaMacrossDataArchive.zip
    UtaMacrossDataArchivePCPatch.zip
  ServerData/
    android/
    db/
    RequestGetFiles.json
```

5. 처음 서버를 시작할 때 Windows 보안 경고가 나타나면 **사설 네트워크**만 체크하고 액세스를 허용합니다.
6. PC와 휴대폰을 같은 Wi-Fi/공유기에 연결하고 휴대폰에서 데이터 설치를 진행합니다. 자동 탐색에 실패하면 프로그램의 `Android 수동 입력` 뒤에 표시된 IP 숫자만 입력합니다. `http://`, `:8000`, `localhost`는 붙이지 않습니다.
7. 완료 전에는 PC 절전·서버 종료를 피합니다. 프로그램은 실행 중 자동으로 절전을 막습니다. 완료 후 서버를 꺼도 되며, 다음 실행에서는 배치된 데이터를 확인하고 자동으로 서버를 시작합니다.

서버는 TCP 8000(파일 전송)과 UDP 8001(자동 탐색)을 사용합니다. 공유기 포트 포워딩은 필요하지 않으며 보안을 위해 설정하지 마세요. 자세한 안내와 문제 해결은 [PC 서버 문서](Tools/Windows/PC_SERVER.md)를 참고하세요.

개발자는 다음 명령으로 서버 배포 ZIP을 만들 수 있습니다. Unity와 Python은 필요하지 않고 Windows의 .NET Framework C# 컴파일러를 사용합니다.

```powershell
.\Tools\Windows\Test-PcServerAssistant.ps1
.\Tools\Windows\Publish-PcServerAssistant.ps1
```

원본 Unity 서버를 사용하려면 [원본 Releases](https://github.com/Xele02/UMO/releases)의 `UMOServer_*_Windows.zip`을 받아 수동으로 압축 해제한 `data` 경로를 지정할 수도 있습니다.

PC가 없으면 원본 안내의 인터넷 다운로드 방식도 가능합니다. 서버 탐색을 취소하고 주소 입력란에 **`umo.xele.org`**를 입력합니다. 원본 서버 운영 상태와 속도의 영향을 받습니다. Wi-Fi 및 충분한 저장 공간을 준비하세요. 다운로드 약 14GB 외에 압축 해제 등 추가 공간이 필요합니다. 자세한 문제 해결은 원본 안내를 참고하세요.

### 이후 한국어판 업데이트와 세이브 보존

이번 베타 이후에는 한국어판을 **삭제하지 말고 새 APK로 덮어 설치**합니다. 배포자는 같은 앱 ID·서명 키를 사용하고 `versionCode`를 증가시킵니다. 이미지 번역만 바꾸는 업데이트에서 세이브·데이터를 초기화하지 않는 방침입니다. 다만 후속 버전의 실제 업데이트 테스트까지 완료된 것은 아니며, 새 게임 데이터가 필요하면 추가 다운로드가 발생할 수 있습니다.

앱 삭제, Android 설정의 데이터 삭제, 다른 서명으로 만든 자체 빌드는 보존 대상이 아닙니다. 업데이트 전에 게임의 계정 내보내기로 프로필과 설정을 별도 보관하세요. 설치 충돌 시 앱을 지우지 말고 앱 ID·서명·버전부터 확인하세요.

## PC용 빌드 방법 — Windows x64 개발판

개발자용 절차입니다. 독립 실행 파일을 만들 수 있지만 일부 그래픽 오류가 남아 있습니다. PC 텍스처 변환과 PC 전용 입력 변경은 Android에 적용하지 않습니다.

### 초보자용 PC 빌드 도우미

명령어를 직접 입력하기 어려운 사용자는 Releases의 `UMO_Kor_For_windows_20260907.zip`을 새 폴더에 풀고, 동봉된 `우타마크로스 오프라인 PC 빌드 도우미 사용방법.pdf`를 읽은 뒤 `UMO_PC_Build_Assistant.exe`를 실행합니다. **도우미가 들어 있는 폴더가 기본 작업 폴더**가 되므로, 쓰기 가능한 빈 폴더에서 시작하고 수십 GB 이상의 여유 공간을 확보하세요.

도우미는 다섯 페이지를 순서대로 안내합니다.

1. Git, Python, Unity 2018.4.36f1 및 .NET Framework 컴파일러를 자동 검사합니다. 없는 프로그램은 화면의 다운로드 버튼으로 설치하고 `설치 상태 새로고침`을 누릅니다.
2. `필요 패키지 설치`를 누르면 전용 Python 환경과 정확한 버전의 변환 패키지를 자동 설치합니다.
3. `Git 클론 및 PC 빌드`를 누르면 한국어판 `develop` 브랜치를 받고 Unity 빌드와 기본 결과 검사를 수행합니다.
4. `ZIP 파일 넣을 폴더 열기`를 누르고 `UtaMacrossDataArchive.zip`과 `UtaMacrossDataArchivePCPatch.zip`을 열린 폴더에 복사합니다. 압축을 직접 풀 필요 없이 `새로고침 및 자동 배치`를 누릅니다.
5. `텍스처 변환 및 최종 검증`을 누릅니다. 완료되면 `게임 폴더 열기 및 키 설정` 버튼이 활성화되고, 이 버튼은 완성된 게임 폴더와 키 설정 프로그램을 함께 엽니다.

각 페이지의 검사가 통과해야 `다음` 버튼이 활성화됩니다. ZIP과 변환 결과는 용량이 크므로 작업 중 도우미를 종료하거나 PC를 절전 상태로 만들지 마세요. [도우미 상세 안내](Tools/Windows/PC_BUILD_ASSISTANT.md)와 아래 개발자용 수동 절차도 함께 유지합니다.

Python 구성 요소 설치, 소스 클론·Unity 빌드, 이미지 변환·검증 중에는 진행률 막대와 현재 단계가 표시됩니다. 변환 단계는 처리 파일 수를 기준으로 실제 진행률을 표시합니다.

Python 설치 시 **Add python.exe to PATH**, `pip`, `py launcher`를 포함하고 64비트 Python 3.10 이상을 사용합니다. Unity는 정확히 **2018.4.36f1**과 Windows Mono 빌드 지원을 설치하고, 한 번 직접 실행해 로그인·라이선스 활성화를 끝낸 뒤 닫습니다. 자세한 설치 화면별 주의사항은 위 상세 안내를 확인하세요.

### 필요한 프로그램

- Windows 64비트 및 [Git for Windows](https://git-scm.com/download/win).
- Unity Hub와 **Unity Editor 2018.4.36f1**: [다운로드](https://unity.com/releases/editor/whats-new/2018.4.36). 라이선스를 활성화하고 에디터를 한 번 실행합니다. 다른 Unity 버전으로 임의 업그레이드하지 마세요.
- Windows Standalone **Mono** 지원. Hub에서 별도 모듈로 표시되면 추가합니다. Windows 빌드에는 Android SDK/JDK/NDK가 필요하지 않습니다.
- [Python 3.12 64비트](https://www.python.org/downloads/windows/): PC 텍스처 변환용입니다. 완성된 게임 실행에는 필요 없습니다.
- PowerShell 및 수십 GB 이상의 추가 여유 공간. 원본 데이터, Unity Library, 빌드와 RGBA 캐시를 함께 보관합니다.

LibVLC 런타임은 저장소에 포함된 Windows 파일을 빌드 도우미가 복사합니다. 시스템에 VLC 플레이어를 설치하는 것으로 빌드 폴더의 DLL 누락을 대신할 수 없습니다.

### 소스 준비

PowerShell에서 실행합니다. 이후 명령도 저장소 최상위에서 실행합니다.

```powershell
git clone --branch develop https://github.com/ccs21/UMO_Kor.git
cd UMO_Kor
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r Tools/Windows/requirements.txt
```

Hub의 **Add project from disk**로 저장소 안의 **Unity 폴더**를 추가합니다. 2018.4.36f1로 열어 최초 패키지 복원·임포트가 끝나면 에디터를 닫습니다. 같은 프로젝트를 에디터와 명령행 빌드에서 동시에 열지 마세요.

### 독립 실행 파일 생성

아래 도우미는 TMP 리소스 준비와 실제 빌드를 별도의 Unity 실행으로 수행하고 결과를 검사합니다. 일반 Build 버튼 대신 도우미를 사용하세요.

```powershell
.\Tools\Build\Build-Windows.ps1
# Unity 설치 경로가 다른 경우
.\Tools\Build\Build-Windows.ps1 -Unity 'E:\Unity\2018.4.36f1\Editor\Unity.exe'
```

결과: **`Unity/Build/Windows/UMO_Kor/UMO_Kor.exe`**. 로그: `Logs/windows-prepare.log`, `Logs/windows-build.log`. 실패 후 이전 EXE가 남아 있어도 빌드 성공으로 간주하지 마세요.

### 데이터와 텍스처 캐시 준비

1. [원본 PC 설치 안내](https://umo.xele.org/getting-started/installation/install-pc/)의 아카이브·PC 패치 링크를 이용합니다.
2. 압축 해제 후 `android`와 PC 패치의 `db`를 다음 위치에 배치합니다. 게임을 종료한 상태에서 작업하세요.

```text
Unity/Build/Windows/UMO_Kor/
  UMO_Kor.exe
  UMO_Kor_Data/       ← Unity 빌드 결과, 이름 변경 금지
  MonoBleedingEdge/
  Data/
    android/
    db/
    Request*.json    ← 빌드 도우미가 복사하는 목록 파일
    WindowsCache/    ← 다음 단계에서 생성
```

3. Windows가 지원하지 않는 Android 압축 텍스처를 **전체 변환**합니다. 원본을 변경하지 않고 별도 캐시를 생성합니다. 시간과 추가 공간이 많이 필요합니다.

```powershell
.\.venv\Scripts\python.exe Tools/Windows/prepare_texture_cache.py --all --workers 4
```

4. 마지막 `Done: bundles=... failures=0`을 확인합니다. 실패 목록은 `Data/WindowsCache/last-report.json`에 기록됩니다. 실패 원인을 해결한 뒤 재실행하면 정상 캐시는 재사용됩니다. 변환 검사 통과가 모든 화면의 정상 표시를 보증하지는 않습니다.
5. `UMO_Kor.exe`를 실행합니다. EXE만 옮기지 말고 DLL과 데이터 폴더를 함께 유지하세요. 경로를 요청하면 `android`와 `db`가 들어 있는 **Data**를 선택합니다.

PC 세이브는 **게임 실행 파일 옆 `UserData` 폴더**에 저장됩니다. 기존 `%USERPROFILE%/AppData/LocalLow/UtaMacross/UtaMacross` 세이브는 새 PC판을 처음 실행할 때 `UserData`로 자동 복사하며, 이미 새 위치에 있는 파일은 덮어쓰지 않습니다. 게임을 옮길 때는 실행 파일과 `UserData` 폴더를 함께 옮기고, 업데이트할 때 `UserData`를 삭제하지 마세요. Android와 PC 사이의 세이브 이전은 이번 베타 지원 범위가 아닙니다.

[Windows 개발·진단 문서](Tools/Windows/README.md)에서 알려진 문제와 테스트 방법을 확인할 수 있습니다. 게임과 함께 빌드되는 `UMO_PC_Settings.exe`에서 키·게임패드 배치를 변경할 수 있습니다. 자세한 사용법은 [PC 설정 안내](Tools/Windows/PC_SETTINGS.md)를 참고하세요.

## 배포자용 Android 릴리스 빌드

서명 키·암호는 Git/Release에 절대 올리지 않습니다. 고정 앱 ID와 버전 코드는 [빌드 도우미](Unity/Assets/Editor/UMOKoreanAndroidBuild.cs)에 정의됩니다. SDK/JDK/NDK 설정 및 키 백업은 [릴리스 관리 문서](Tools/Build/README.md)를 참고하세요.

## 라이선스 정보

원본은 **Copyright (c) 2022 Xele02**, [MIT License](LICENSE)입니다. 포크의 자체 변경 코드와 문서도 MIT로 제공하며 원본 저작권·라이선스 고지를 유지합니다.

제3자 라이브러리·폰트·리소스는 각자의 라이선스를 따릅니다. LibVLC/LibVLCSharp, CRI 관련 코드, TextMesh Pro, 폰트 등은 해당 파일의 고지를 확인하세요. MIT가 원본 게임의 캐릭터·상표·음악·영상·이미지·데이터에 대한 권리를 부여하는 것은 아닙니다. 게임 데이터의 권리는 각 권리자에게 있습니다.

## 원본과 달라진 점 및 제한

- 한국어 텍스트 내장, 한국어 기본 시작, 앱 이름 **우타마크로스**.
- Android 배포용 앱 ID·서명 분리. 원본 일본어판 자동 세이브 승계 미지원.
- 번역 문자열 줄바꿈 처리 보완. 이미지 일본어는 별도 작업이 남아 있음.
- Windows 독립 실행 빌드 도우미, 앱 옆 데이터 경로, PC 번들·텍스처·셰이더 보완.
- PC 롱노트 끝 flick에서 끝 타이밍에 키를 놓는 판정 보완과 튜토리얼 안내 리소스 로드 수정. 실제 입력 및 모든 안내 경로는 추가 검증 필요.
- 한국어 PC 서버 도우미 추가. 원본 데이터 ZIP과 PC 패치를 자동 배치하고 LAN IP 안내, 자동 탐색 및 스트리밍 전송을 처리합니다. 게임 데이터 자체와 공식 로그인 보너스 자동 설치는 포함하지 않습니다.
