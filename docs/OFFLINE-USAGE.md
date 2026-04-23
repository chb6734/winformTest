# 오프라인 환경에서 사용하기 (기존 프로젝트 무간섭)

## 핵심 원칙
- 대상 앱 프로젝트(`.csproj`/`.sln`)는 **한 줄도 수정하지 않습니다**
- NuGet 다운로드 없음
- `e2e.exe` 한 덩어리 폴더를 들고 다니기만 하면 됨

## Step 1 — 인터넷 있는 개발 머신에서 배포 폴더 생성 (1회)

```powershell
git clone https://github.com/chb6734/winformTest C:\src\e2e
cd C:\src\e2e

# (선택) ffmpeg.exe를 tools/ 에 두면 같이 번들됨
# winget install Gyan.FFmpeg 후 tools/ffmpeg.exe 복사

# self-contained (target에 .NET 8 런타임 없어도 OK, ~150MB)
powershell -File build/publish-offline.ps1

# 또는 framework-dependent (target에 .NET 8 설치되어 있으면, ~10MB)
powershell -File build/publish-offline.ps1 -FrameworkDependent

# 오프라인 환경으로 옮길 ZIP 생성
Compress-Archive dist/e2e -DestinationPath e2e-offline.zip
```

## Step 2 — 오프라인 머신에 배포

1. `e2e-offline.zip`을 USB/내부망으로 복사 후 압축 해제, 예: `C:\tools\e2e\`
2. 끝. 레지스트리, 환경변수, 설치 과정 없음

## Step 3 — 기존 프로젝트 옆에 테스트 폴더 생성

기존 프로젝트가 `C:\MyProject\` 이고 빌드 결과물이 `C:\MyProject\bin\Debug\net8.0-windows\MyApp.exe` 라면:

```
C:\MyProject\
├── MyApp\                        ← 기존 (손대지 않음)
│   ├── MyApp.csproj
│   └── bin\...\MyApp.exe
└── e2e-tests\                    ← 신규 추가 (아무것도 안 들어가도 됨)
    ├── login.yaml
    └── checkout.yaml
```

**`login.yaml` 예시:**
```yaml
name: 로그인 시나리오
app:
  path: ../MyApp/bin/Debug/net8.0-windows/MyApp.exe
steps:
  - action: screenshot
    name: initial
    description: 앱 초기 상태 캡처
  - action: fill
    target: "#txtUser"
    value: admin
    description: 관리자 계정으로 로그인
  - action: click
    target: "#btnLogin"
    description: 로그인 버튼 클릭
  - action: assert.text
    target: "#lblStatus"
    value: 환영합니다
    description: 환영 메시지 표시 확인
```

## Step 4 — 실행

```powershell
cd C:\MyProject
# 헤드리스
C:\tools\e2e\e2e.exe run e2e-tests\login.yaml

# 대시보드 모드 (실시간 시각화)
C:\tools\e2e\e2e.exe ui e2e-tests\login.yaml
```

결과: `C:\MyProject\out\runs\20260423_xxxxxx_로그인_시나리오\`
- `report.json` — 스텝별 결과
- `frames\` — PNG 스크린샷
- `video.mp4` — (ffmpeg 있을 때)

## PATH 등록 (선택)

매번 풀 경로 치기 싫으면:
```powershell
# PowerShell 프로필에 추가
Add-Content $PROFILE '$env:PATH += ";C:\tools\e2e"'
. $PROFILE
# 이후 어디서든:
e2e run login.yaml
```

## 기존 빌드 파이프라인에 끼워넣기

기존 MSBuild 타겟 수정 없이 **별도 배치 파일**로 호출:

**`test-e2e.bat`:**
```batch
@echo off
REM 1. 대상 앱 빌드 (기존 방식 그대로)
call build-myapp.bat

REM 2. E2E 테스트
C:\tools\e2e\e2e.exe run e2e-tests\smoke.yaml
if %ERRORLEVEL% neq 0 (
    echo E2E 실패
    exit /b 1
)
echo E2E 통과
```

## 주의사항

- 대상 앱이 .NET Framework 3.5 기반이라도 **e2e.exe는 .NET 8 self-contained**로 동작 (out-of-process 제어이므로 CLR 격리).
- UIA가 인식 못하는 커스텀 컨트롤이 있다면 `e2e.exe dump MyApp.exe`로 UI 트리 덤프하여 올바른 셀렉터 확인.
- 오프라인 머신에서 Dashboard 모드 사용 시 Windows Defender SmartScreen이 서명 없는 exe를 차단할 수 있음 → 우클릭 > 속성 > "차단 해제".
