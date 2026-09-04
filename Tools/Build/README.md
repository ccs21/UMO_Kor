# 릴리스 관리

## 고정 배포 식별자

- 패키지: `com.ccs21.umokor`
- 표시 이름: 우타마크로스
- 첫 베타: `1.1.16-ko-beta.1`, versionCode `10001`
- 현재 베타: `1.1.16-ko-beta.6`, versionCode `10006`
- Git 태그: `v1.1.16-ko-beta.6`
- 서명 alias: `umo-kor-release`

후속 릴리스에서 앱 ID와 서명 키를 바꾸지 말고 versionCode를 증가시키세요. 이미지 번역 업데이트에 세이브 삭제나 데이터 경로 변경을 섞지 않습니다. 원본 일본어판 및 `com.ccs21.UMOKorTest` 개발판과는 별도 앱입니다. 기존 설치본을 제거하도록 안내하지 마세요.

Android의 업데이트 조건은 [앱 ID 문서](https://developer.android.com/build/configure-app-module)와 [앱 서명 문서](https://developer.android.com/studio/publish/app-signing)를 참고하세요. 동일 서명/ID는 필요조건이며 세이브 호환성을 코드 수준에서 유지하고 기기에서 업데이트 검사도 해야 합니다.

## 빌드 도구

이 프로젝트에서 사용하는 버전을 기준으로 준비합니다.

- Unity 2018.4.36f1 + Android Build Support + OpenJDK 8.
- Android SDK: API 28 플랫폼, Build Tools 28.0.3, Platform Tools.
- Android NDK r16b. 최신 NDK를 대신 사용하지 않습니다.
- SDK는 [Android 개발 도구](https://developer.android.com/studio#command-tools), NDK는 [이전 NDK 다운로드](https://developer.android.com/ndk/downloads/older_releases)에서 구할 수 있습니다.

Unity Hub에서 프로젝트 `Unity`를 한 번 열어 임포트를 마치고 닫습니다. 빌드 중에는 같은 프로젝트를 열지 않습니다. Android는 IL2CPP/ARMv7+ARM64 프로젝트 설정을 사용합니다.

```powershell
# 예시 경로를 실제 설치 위치로 변경하세요.
# 최초 서명 키 생성은 한국어 배포 관리자만 한 번 실행합니다.
.\Tools\Build\Build-AndroidRelease.ps1 `
  -Sdk 'D:\Android\Sdk' `
  -Ndk 'D:\Android\android-ndk-r16b' `
  -Jdk 'C:\Program Files\Unity\Hub\Editor\2018.4.36f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK' `
  -SigningDirectory 'D:\PrivateSigning\UMO_Kor' `
  -InitializeSigning
```

이후에는 **`-InitializeSigning`을 빼고 같은 서명 폴더**로 실행합니다. 이미 키가 있는 경우 초기화는 실패하게 되어 있습니다. 새 PC에서 임의로 새 키를 만들면 기존 사용자는 덮어 업데이트할 수 없습니다.

출력: `Unity/Build/Android/UMO_Kor-1.1.16-ko-beta.6.apk`. 준비 로그는 `Logs/android-prepare.log`, 빌드 로그는 `Logs/android-release.log`입니다. 공개 빌드는 `BuildOptions.None`이며 서명 정보가 없으면 중단됩니다. 개발용 `BuildAndroid`/`BuildParallelTest` 결과를 릴리스에 올리지 마세요.

## 서명 키 보관 및 복구

서명 폴더는 저장소 밖에 두고 현재 Windows 계정과 SYSTEM만 접근하도록 설정됩니다. `umo-kor-release.jks`는 개인 키, `release.password.dpapi`는 암호화된 키 암호입니다. 둘 다 Git/Release/채팅/로그에 공개하면 안 됩니다.

**DPAPI 파일은 현재 Windows 사용자/PC에 묶여 있습니다. 파일 두 개를 USB에 복사하는 것만으로 다른 PC에서 복구할 수 있다고 보장하지 않습니다.** PC 포맷 전에 키 암호를 본인 암호 관리자로 안전하게 옮기고, JKS도 별도 암호화 백업을 보관하세요. 암호를 화면이나 명령 기록에 출력하지 마세요. 키를 잃으면 기존 설치본과 호환되는 APK를 더 이상 만들 수 없습니다.

복구 PC에서는 안전하게 보관한 원래 JKS를 복사하고, `Read-Host -AsSecureString`으로 원래 암호를 입력받아 `ConvertFrom-SecureString`으로 새 PC의 `release.password.dpapi`를 만듭니다. **키 자체를 재생성하지 않습니다.** 복구 후 서명 인증서 SHA-256이 공개된 이전 APK와 같은지 검사합니다.

## 배포 전 검사

SDK Build Tools의 `apksigner.bat verify --verbose --print-certs <APK>`로 서명을 검사합니다. `aapt.exe dump badging <APK>`로 다음을 확인하세요.

아래 도우미는 위 검사와 함께 저장소의 공개 인증서 지문(`release-certificate.sha256`) 일치 여부를 검사합니다. 공개 지문은 개인 키나 암호가 아니며, 새 키를 정당화하려고 임의로 바꾸면 안 됩니다.

```powershell
.\Tools\Build\Verify-AndroidRelease.ps1 `
  -Apk '.\Unity\Build\Android\UMO_Kor-1.1.16-ko-beta.6.apk' `
  -BuildTools 'D:\Android\Sdk\build-tools\28.0.3' `
  -Jdk 'C:\Program Files\Unity\Hub\Editor\2018.4.36f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK'
```

- package `com.ccs21.umokor`, 올바른 versionName/versionCode.
- 앱 표시 이름, ARMv7/ARM64 네이티브 코드.
- `application-debuggable`이 없어야 함.
- 인증서가 이전 한국어 릴리스와 일치해야 함.

실기에서는 원본 앱을 건드리지 말고 배포판 신규 설치, 서버 데이터 설치, 한국어 표시를 점검합니다. 후속판에서는 이전 배포판의 테스트 프로필을 백업한 뒤 **삭제 없이 업데이트**하여 세이브와 다운로드 데이터를 확인합니다. 빌드/서명 검사만으로 실기 테스트를 통과했다고 표시하지 마세요.

커밋 대상은 소스·번역·문서뿐입니다. `git diff --cached --name-only`를 검토하고 아카이브, PC 패치, 생성된 캐시, 개인 세이브, 키/암호가 없는지 확인합니다. APK는 Git 커밋 대신 GitHub **Pre-release** 자산으로 올립니다. 릴리스 노트에는 알려진 오류, 이미지 미번역, 실기 검사 여부를 기록합니다.
