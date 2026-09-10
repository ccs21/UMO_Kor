# 이미지 한글화 수정본

`PC` 폴더에는 개발용 PC 빌드에서 F8로 덤프하고 실제 화면에서 검수한 PNG만 넣습니다.
파일명에는 원본 텍스처 이름, 해상도와 원본 픽셀 해시가 포함되어 있으므로 이름을
바꾸지 않습니다. 캔버스 크기와 각 요소의 위치도 원본과 동일하게 유지합니다.

`manifest.json`은 각 이미지의 원본 번들 및 텍스처 대응표입니다. Android와 Windows
릴리스 빌드는 이 43개 검수본을 Unity 리소스로 내장하고, 원본 번들이 로드될 때 고정된
대상에만 적용합니다. 배포 빌드에는 외부 이미지 폴더와 F8/F9 기능이 포함되지 않습니다.

작업 중 덤프와 즉시 교체가 필요할 때만 Windows 빌드에
`-ImageTranslationDevelopment`를 지정합니다. 새 검수본을 추가한 뒤에는 원본 추출
manifest를 기준으로 `Tools/Localization/build_image_override_manifest.py`를 실행하고,
생성된 대응표와 화면 결과를 검토한 뒤 커밋합니다.
