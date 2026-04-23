# 오프라인 배포용 self-contained publish.
# 결과물: ./dist/e2e/ 안에 e2e.exe + E2ETest.Dashboard.exe + 필요한 DLL 전부 + ffmpeg.exe (있으면).
# 대상 환경에 .NET 런타임이 없어도 동작 (self-contained).
# 인터넷 없이 ZIP으로 복사해서 바로 실행 가능.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "dist/e2e",
    [switch]$FrameworkDependent  # -FrameworkDependent 지정 시 .NET 8 런타임이 이미 설치된 환경용 (용량 작음)
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

$publishArgs = @("-c", $Configuration, "-r", $Runtime, "--self-contained")
if ($FrameworkDependent) { $publishArgs = @("-c", $Configuration, "-r", $Runtime, "--self-contained", "false") }

Write-Host "[1/3] Publishing CLI..." -ForegroundColor Cyan
dotnet publish src/E2ETest.Cli/E2ETest.Cli.csproj @publishArgs -o "$OutputDir" /p:PublishSingleFile=false

Write-Host "[2/3] Publishing Dashboard..." -ForegroundColor Cyan
$dashTemp = "$env:TEMP\e2e-dashboard-publish-$((Get-Date).Ticks)"
dotnet publish src/E2ETest.Dashboard/E2ETest.Dashboard.csproj @publishArgs -o $dashTemp /p:PublishSingleFile=false
# Dashboard의 DLL을 CLI 폴더로 합침 (중복 DLL은 덮어쓰기 OK — 같은 버전)
Copy-Item "$dashTemp\*" $OutputDir -Recurse -Force
Remove-Item $dashTemp -Recurse -Force

# FFmpeg bundling (선택)
$ffmpegSrc = "tools/ffmpeg.exe"
if (Test-Path $ffmpegSrc) {
    Write-Host "[3/3] Bundling ffmpeg.exe..." -ForegroundColor Cyan
    $ffmpegDest = "$OutputDir/tools"
    New-Item -ItemType Directory -Path $ffmpegDest -Force | Out-Null
    Copy-Item $ffmpegSrc $ffmpegDest -Force
} else {
    Write-Host "[3/3] ffmpeg.exe not in tools/ — 녹화는 되지만 MP4 합성은 수동. 오프라인 환경에 ffmpeg.exe를 tools/에 먼저 두세요." -ForegroundColor Yellow
}

# 샘플 YAML 복사 (참고용)
Copy-Item "samples/tests/login.yaml" "$OutputDir/sample-login.yaml" -Force

# 사용법 README
@"
# E2ETest Offline Package

이 폴더를 **대상 머신의 어디든** 복사하면 됩니다. 다른 파일 설치 필요 없음.

## 사용법

``````
# 기본 실행 (헤드리스)
.\e2e.exe run <test.yaml>

# 대시보드 모드
.\e2e.exe ui <test.yaml>

# 녹화 강제
.\e2e.exe record <test.yaml>

# UIA 트리 덤프 (셀렉터 찾을 때)
.\e2e.exe dump <대상앱.exe>
``````

## YAML 예시

sample-login.yaml 파일 참고. app.path는 테스트할 실행파일 경로.

## FFmpeg

tools/ffmpeg.exe가 있으면 video.mp4 자동 생성. 없어도 프레임 PNG는 보존됩니다.

## 기존 프로젝트에 영향 주지 않는 사용법

1. 기존 프로젝트 빌드 (`dotnet build` 또는 MSBuild) — 변경 없음
2. 이 폴더(예: ``C:\tools\e2e``)를 어디든 복사
3. 기존 프로젝트 옆에 ``e2e-tests`` 폴더 만들고 YAML 스크립트 작성
4. ``C:\tools\e2e\e2e.exe run e2e-tests\login.yaml`` 실행
5. 결과는 ``<현재디렉토리>/out/runs/<timestamp>_<testname>/`` 에 생성

기존 프로젝트의 csproj/sln/코드는 **한 줄도 건드리지 않습니다**.
"@ | Set-Content "$OutputDir/README.txt" -Encoding UTF8

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "배포 폴더: $repoRoot\$OutputDir"
Write-Host "크기: $((Get-ChildItem $OutputDir -Recurse | Measure-Object Length -Sum).Sum / 1MB) MB"
Write-Host ""
Write-Host "대상 머신에 복사하려면:"
Write-Host "  Compress-Archive $OutputDir -DestinationPath e2e-offline.zip"
Write-Host "  그 후 ZIP을 대상 머신으로 전송 → 압축 해제 → e2e.exe 실행"
