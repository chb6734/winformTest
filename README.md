# E2ETest — Playwright-style E2E testing for .NET WinForms/WPF

.NET Framework 3.5부터 최신 .NET까지의 WinForms/WPF 데스크탑 앱을 자동으로 실행하고, 사용자 시나리오대로 조작하며, 성공/실패를 판정하고 과정을 동영상으로 남기는 E2E 테스트 프레임워크.

**지원 대상**
- 대상 앱: .NET Framework 3.5 ~ 최신 .NET (WinForms, WPF)
- 테스트 언어: C#, VB (YAML 선언형도 지원)
- OS: Windows 7 이상 (UIAutomationClient 내장 필요)

## 특징

| 기능 | 설명 |
|------|------|
| Playwright 스타일 Fluent API | `app.Window("로그인").Find("#btnLogin").Click()` |
| YAML 선언형 | 비개발자도 작성 가능한 간단한 step 나열 |
| UIA + Win32 하이브리드 | UIA 실패 시 Win32 API로 폴백, 구형 커스텀 컨트롤 호환 |
| 자동 녹화 | 스크린샷 시퀀스 → FFmpeg로 MP4 자동 합성 |
| 실시간 대시보드 (`e2e run --ui`) | WPF UI에서 스텝 진행, 실시간 스크린샷, 로그 확인 |
| Auto-waiting | 요소가 나타날 때까지 자동 폴링 (기본 10초) |
| 구조화된 리포트 | JSON 리포트 + 스텝별 스크린샷 보존 |

## 빠른 시작

### 1) 빌드

```powershell
dotnet build E2ETest.sln -c Debug
```

### 2) 샘플 앱 빌드

```powershell
dotnet build samples/WinFormsSampleCSharp/WinFormsSampleCSharp.csproj -c Debug
```

### 3) 테스트 실행

```powershell
# 헤드리스 (기본)
dotnet run --project src/E2ETest.Cli -- run samples/tests/login.yaml

# 대시보드 모드
dotnet run --project src/E2ETest.Cli -- run --ui samples/tests/login.yaml
# 또는
dotnet run --project src/E2ETest.Cli -- ui samples/tests/login.yaml
```

출력 위치: `out/runs/{timestamp}_{test_name}/`
- `report.json` — 스텝별 결과
- `frames/` — 모든 스크린샷
- `video.mp4` — 합성 영상 (FFmpeg 존재 시)

## YAML 테스트 예시

```yaml
name: 로그인 성공 시나리오
app:
  path: ./MyApp.exe
  args: "--test-mode"
steps:
  - action: fill
    target: "#txtUser"
    value: admin
  - action: fill
    target: "#txtPassword"
    value: pass1234
  - action: click
    target: "#btnLogin"
  - action: assert.text
    target: "#lblStatus"
    value: 환영합니다
  - action: screenshot
    name: welcome
```

## Fluent API 예시 (C#)

```csharp
using E2ETest.Core.Api;

[E2ETest(AppPath = "./MyApp.exe")]
public void Login_Success(IApp app)
{
    app.MainWindow
        .Find("#txtUser").Fill("admin");
    app.MainWindow
        .Find("#txtPassword").Fill("pass1234");
    app.MainWindow
        .Find("#btnLogin").Click();
    app.MainWindow
        .Find("#lblStatus").ExpectText("환영합니다");
    app.Screenshot("welcome");
}
```

## Fluent API 예시 (VB)

```vb
<E2ETest(AppPath := "./MyApp.exe")>
Public Sub Login_Success(app As IApp)
    app.MainWindow.Find("#txtUser").Fill("admin")
    app.MainWindow.Find("#txtPassword").Fill("pass1234")
    app.MainWindow.Find("#btnLogin").Click()
    app.MainWindow.Find("#lblStatus").ExpectText("환영합니다")
    app.Screenshot("welcome")
End Sub
```

## 셀렉터 문법

```
#autoId                                  → AutomationId
text=로그인                               → Name 부분 일치
name=사용자명                            → Name 정확 일치
class=Button                             → ClassName
Button[text=확인]                         → ControlType + 속성
Window[title=로그인] Button[text=확인]      → 중첩 (공백 구분)
```

## Actions

| Action | 인자 | 설명 |
|--------|------|------|
| `click` | target | 요소 클릭 (Invoke/Toggle 패턴, 없으면 좌표 클릭) |
| `type` | target, value | 현재 포커스 텍스트 입력 |
| `fill` | target, value | 전체 선택 후 텍스트 교체 (ValuePattern 우선) |
| `focus` | target | 포커스 |
| `wait` | value (ms) | 대기 |
| `screenshot` | name | 명시적 스냅샷 |
| `assert.visible` | target | 화면에 보이는지 |
| `assert.text` | target, value | 텍스트에 값 포함 |
| `assert.enabled` | target | 활성 상태 |

## 아키텍처

```
┌─────────────────────────────────────────┐
│  e2e CLI (E2ETest.Cli)                  │  <- 진입점
└──────────┬──────────────────────────────┘
           │
           ├──→ YamlLoader / ScriptLoader / AssemblyTestLoader
           │                                   (E2ETest.Scripting)
           │
           ▼
┌─────────────────────────────────────────┐
│  Runner (E2ETest.Core)                  │  <- TestCase 실행
│  ├─ AppLauncher                         │
│  ├─ HybridBackend (UIA + Win32)         │
│  ├─ Fluent API (IApp, IWindow, IElement)│
│  └─ ScreenshotRecorder + VideoComposer  │
└──────────┬──────────────────────────────┘
           │   (RunnerEvent stream via Named Pipe)
           ▼
┌─────────────────────────────────────────┐
│  WPF Dashboard (E2ETest.Dashboard)       │  <- 실시간 시각화
└─────────────────────────────────────────┘
```

## 프로젝트 구조

```
winformTester/
├── src/
│   ├── E2ETest.Core/          # net35;netstandard2.0;net8.0-windows
│   ├── E2ETest.Scripting/     # YAML + Roslyn C#/VB 로더
│   ├── E2ETest.Ipc/           # Named Pipe 채널
│   ├── E2ETest.Cli/           # e2e.exe
│   └── E2ETest.Dashboard/     # WPF 대시보드
├── samples/
│   ├── WinFormsSampleCSharp/        # net8.0-windows C#
│   ├── WinFormsSampleCSharp.Net35/  # .NET 3.5 호환성 데모
│   ├── WpfSampleVb/                 # VB WPF 계산기
│   └── tests/                       # YAML/.csx/.vbx
├── tests/E2ETest.Tests/        # xunit 단위 테스트
├── tools/                      # ffmpeg.exe 배치 위치
└── .claude/                    # 하네스 설정 (agents + skills)
```

## FFmpeg 설치

동영상 합성용. 둘 중 하나:
1. `tools/ffmpeg.exe`에 정적 빌드 배치
2. PATH에 `ffmpeg` 설치 (`choco install ffmpeg` 등)

FFmpeg 없이 실행해도 테스트는 정상 진행되며, 프레임은 `out/.../frames/`에 보존됩니다.

## 로드맵

- [x] UIA + Win32 하이브리드 백엔드
- [x] YAML 선언형 테스트
- [x] Fluent API (C#/VB)
- [x] 스크린샷 + FFmpeg MP4 합성
- [x] WPF 대시보드 + Named Pipe
- [ ] 레코더 (사용자 조작 → 코드 자동 생성)
- [ ] 시각 회귀 (pixel diff)
- [ ] CI 통합 (GitHub Actions/Azure DevOps report formats)
- [ ] 병렬 실행
- [ ] Allure / Playwright HTML report 호환 출력

## 라이선스

MIT
