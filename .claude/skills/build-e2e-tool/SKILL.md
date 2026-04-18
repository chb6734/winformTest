---
name: build-e2e-tool
description: .NET WinForms/WPF용 Playwright 스타일 E2E 테스트 툴을 개발/확장/유지보수한다. "E2E 테스트 툴", "e2e:ui", "WinForms 테스트", "WPF 자동화", "UI 자동화 툴", "테스트 녹화" 등의 요청 시 반드시 이 스킬을 트리거할 것. 초기 구축뿐 아니라 "다시 실행", "재실행", "버그 수정", "기능 추가", "업데이트" 등 후속 작업도 이 스킬로 처리.
---

# Build E2E Tool — 오케스트레이터

.NET Framework 3.5 ~ 최신 .NET의 WinForms/WPF 앱을 자동으로 실행하고 사용자 시나리오대로 조작하며, 성공/실패를 판정하고 전체 과정을 영상으로 남기는 Playwright 스타일 E2E 테스트 툴을 개발·확장하는 오케스트레이터.

## 팀 구성

- **architect**: 시스템 설계, 인터페이스 계약
- **automation-engineer**: UIA + Win32 자동화 엔진, 프로세스/셀렉터
- **script-engineer**: YAML DSL + Roslyn C#/VB + Fluent API
- **media-ui-engineer**: 녹화 엔진 + WPF 대시보드
- **qa-release-engineer**: 통합 QA, 샘플 앱, NuGet/GitHub 배포

실행 모드: **에이전트 팀** (TeamCreate + 파일 기반 산출물 전달)

## Phase 0: 컨텍스트 확인

작업 시작 전 기존 상태를 확인해 실행 모드를 결정한다.

- `_workspace/` 없음 → **초기 실행** (전체 Phase 순차 진행)
- `_workspace/` + 사용자가 특정 모듈 수정 요청 → **부분 재실행** (해당 담당자만 재호출)
- `_workspace/` + 새 기능 요청 → **확장** (architect가 설계 업데이트 → 담당자 추가 구현)
- `_workspace/` + 버그 리포트 → **수정** (QA가 재현 → 담당자 수정 → 재검증)

## Phase 1: 아키텍처 설계 (architect)

산출물: `_workspace/architecture/*.md`
- 인터페이스 정의, 데이터 스키마, 모듈 맵, ADR

## Phase 2: 병렬 모듈 구현 (automation, script, media-ui 동시)

각 담당자는 architect의 계약을 읽고 자기 모듈을 구현한다. SendMessage로 실시간 질의응답.

- automation-engineer → `src/E2ETest.Core/Automation/`, `Process/`, `Selector/`, `Actions/`
- script-engineer → `src/E2ETest.Core/Api/`, `Scripting/`, `Model/`
- media-ui-engineer → `src/E2ETest.Core/Recording/`, `src/E2ETest.Dashboard/`, `src/E2ETest.Ipc/`

## Phase 3: CLI + 통합 (script-engineer + media-ui-engineer 주도)

- `src/E2ETest.Cli/` 프로젝트에서 `e2e run`, `e2e:ui`, `e2e record` 명령 통합
- 러너와 대시보드를 Named Pipe로 연결

## Phase 4: QA + 샘플 + 배포 (qa-release-engineer)

- 샘플 앱 빌드, 스모크 테스트 실행, NuGet 패키징
- GitHub 저장소 생성 및 초기 push (사용자 확인 후)

## 데이터 전달 프로토콜

- **파일 기반 (중간 산출물)**: `_workspace/{phase}/{agent}_{artifact}` — 감사 추적용 보존
- **태스크 기반 (조율)**: TaskCreate로 모듈별 작업 추적, 완료 시 TaskUpdate
- **메시지 기반 (실시간)**: SendMessage로 인터페이스 질문/버그 리포트
- **최종 산출물**: `src/`, `samples/`, `tests/` (사용자에게 노출), `.github/`, `README.md`, `LICENSE`

## 에러 핸들링

- 빌드/테스트 실패: 1회 재시도, 재실패 시 해당 산출물을 "실패 상태"로 표시하고 리포트에 명시, 다른 Phase는 계속
- 설계 충돌: architect가 중재, 해결 불가 시 사용자에게 질문
- 외부 도구 부재(FFmpeg, gh CLI): 자동 설치 시도 후 실패 시 사용자에게 명확한 설치 안내

## 테스트 시나리오

### 정상 흐름
"WinForms C# 앱의 로그인 성공 시나리오를 YAML로 테스트하고 영상 남기기"
→ Phase 1~4 순차 실행, YAML 테스트 작성, 실제 앱 실행/조작/녹화, MP4 생성

### 에러 흐름
"요소를 찾을 수 없음 에러가 난다"
→ Phase 0에서 재현 조건 확인, automation-engineer가 UIA 트리 덤프 기능으로 디버깅 자료 추가, 재실행

## 후속 작업 키워드

- "다시 실행", "재실행", "업데이트", "수정", "보완", "버그", "개선"
- "~만 다시", "이전 결과 기반으로"
- "기능 추가", "지원 확장"
