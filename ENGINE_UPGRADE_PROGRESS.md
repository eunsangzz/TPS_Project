# Unity 엔진 업그레이드 오류 해결 기록

최종 업데이트: 2026-09-29

## 기준 및 진행 방식

- Unity 프로젝트: `TpsProject/`
- 엔진 버전: `6000.3.4f1 → 6000.6.3f1`
- 원인 하나를 수정하고 검증한 뒤 결과를 공유한다. 다음 원인은 별도 작업으로 진행한다.
- 현재 엔진·패키지 버전을 기준으로 복구하며, 기존 코드·씬 변경을 보존한다.
- 기능 추가, 밸런스 변경, 구형 API 경고 일괄 정리는 이번 오류 수정에 섞지 않는다.

## 현재 상태

게임 및 에디터 C# 코드에서 컴파일 오류는 확인되지 않았다. 다만 **IL 후처리기(ILPP) 실행 실패로 Unity 전체 컴파일은 완료되지 않았다.** Play 진입 및 실제 게임 동작은 아직 검증하지 않았다.

## 다른 컴퓨터로 전달하는 작업 범위

- 엔진 업그레이드와 API 오류 수정뿐 아니라, 사용자가 요청한 현재 Unity 작업 전체를 함께 전달한다.
- 기존 HUD·적 AI·전투 및 스테이지 밸런스 변경, 씬·재질, 한국어 폰트, 사운드와 각 `.meta` 파일을 포함한다. 이번 전달 과정에서 게임 동작을 추가로 수정하지 않는다.
- 저장소 루트 `.gitignore`에 실제 프로젝트 경로인 `TpsProject/`의 캐시 제외 규칙을 추가한다. 이미 추적되던 `Library`, `Logs`, `Temp`, `UserSettings`, `obj`는 로컬 파일을 보존하고 Git 추적만 해제한다.
- 포트폴리오 출력물과 `.codex_tmp/`는 이번 전달 대상이 아니다.
- Mac에서는 저장소를 별도로 clone하고 Unity Hub에서 `TpsProject/` 폴더를 Unity `6000.6.3f1`로 연다. 캐시는 해당 컴퓨터에서 새로 생성하며 Windows의 `Library` 폴더를 복사하지 않는다.
- 현재 검증 환경은 Windows / Unity `6000.6.3f1`이다. 이전 컴파일 이후 확인 대상 소스·설정 205개 파일의 내용은 바뀌지 않았으며, 새 에셋의 `.meta` 누락과 직접 의존 패키지의 manifest/lock 버전 불일치는 없었다.
- Mac에서의 임포트·Play 검증과 최종 Windows 빌드는 아직 수행하지 않았다. 다음 엔진 오류 해결 작업은 아래 ILPP 실행 실패 확인이다.

## 완료한 작업

### 1. GetInstanceID 사용으로 발생한 CS0619 오류 3곳 수정

| 위치 | 변경 내용 |
|---|---|
| `TpsProject/Assets/Scripts/GameAuthDebugUI.cs:44` | 창의 정수 ID에 `GetEntityId().GetHashCode()` 사용 |
| `TpsProject/Assets/Scripts/EnemyAI.cs:789` | 자신의 ID를 `EntityId ownId = GetEntityId()`로 저장 |
| `TpsProject/Assets/Scripts/EnemyAI.cs:800` | 적의 포위 위치 순서를 계산할 때 `EntityId`끼리 비교 |

- Unity 6000.6.3f1에 포함된 API를 확인했다. `EntityId`의 정수 변환도 오류로 처리되므로 단순 정수 변환은 사용하지 않았다.
- Unity가 생성한 컴파일 설정으로 C# 컴파일러를 직접 실행해 종료 코드 0을 확인했다.
- 로그인 디버그 창과 근접 적 포위 동작의 실제 실행 검증은 남아 있다.
- `FindFirstObjectByType`, `FindObjectsSortMode` 등 기존 구형 API 경고는 유지했다.
- 검증 로그: `TpsProject/Logs/GetEntityIdCSharpCompile.log`

### 2. Bee 빌드 캐시 잠금 오류 처리

- 기존 증상: `Library/Bee/1900b0aE-inputdata.json` 접근 시 `Win32 IO returned 1224`, `Unable to create timestamp file` 발생.
- 실행 중인 Unity Editor·Bee 프로세스가 없었고, Windows 잠금 조회에서도 해당 파일들의 점유 프로세스가 확인되지 않았다. 최초 잠금 주체는 특정하지 못했다.
- 기존 `Library/Bee`를 아래 위치에 백업하고 새 캐시를 생성했다.
  - `TpsProject/Library/Bee.before-lock-repair-20260929-223818/`
- 재컴파일 로그에서 위 잠금 오류는 재발하지 않았다.
- 새 Bee 산출물의 `Assembly-CSharp.dll`, `Assembly-CSharp-Editor.dll` 생성과 C# 오류 0건을 확인했다.
- 검증 전후 해시를 비교한 소스·씬·설정 등 205개 파일은 변경되지 않았다.
- 검증 로그: `TpsProject/Logs/BeeLockRepairCompile.log`
- 비교 기준: `TpsProject/Logs/BeeLockRepair-source-before.json`

## 다음 한 가지 작업: ILPP 실행 실패 해결

최근 Unity 배치 컴파일은 아래 오류로 종료 코드 1을 반환했다.

```text
IL Post Processor runner process failed to start
ILPPTrigger: Can't find file \\.\pipe\unity-ilpp-77ee9f1ec22d1afddad6d1afa8c1f398
Script Compilation Error for: ILPP-Configuration
```

- ILPP 프로세스 시작 실패와 통신 파이프 생성 실패의 원인을 확인한다.
- 확인된 원인만 수정한 뒤 Unity 전체 재컴파일을 수행한다.
- 완료 기준: ILPP 단계를 포함한 컴파일 성공, 정상 종료, 최신 스크립트 어셈블리 로드 확인.
- 이번 기록 시점에는 ILPP 원인 분석 및 수정은 수행하지 않았다.

## 후속 검증 순서

| 순서 | 작업 | 완료 기준 |
|---|---|---|
| 1 | ILPP 및 추가 컴파일·패키지 오류 처리 | Unity 전체 컴파일 성공, Play 진입 가능 |
| 2 | `Login → MainScene` 실행 흐름 확인 | 씬 전환·초기화 정상, 누락 참조·예외 없음 |
| 3 | 기본 플레이 확인 | 이동·카메라 → 사격·피격 → 적 이동·공격 → 스테이지·결과 UI 정상 동작 |
| 4 | 렌더링 확인 | 사용하는 맵·캐릭터·UI의 재질, 조명, 효과 정상 표시 |
| 5 | 남은 구형 API 경고 정리 및 Windows 빌드 | 관련 경고 해소, 빌드 성공 및 실행 확인 |

플레이 검증을 막는 렌더링 문제는 발견 즉시 앞당겨 처리한다. 후속 항목은 확정된 오류 목록이 아니라 검증 순서다.

## 작업 결과 기록 형식

다음 작업부터 아래 항목을 갱신한다.

- 수정한 원인과 변경 범위
- 검증 방법 및 결과, 로그 위치
- 미검증 항목과 남은 문제
- 다음에 처리할 한 가지 작업

로그와 캐시 백업은 현재 로컬 작업 환경의 경로이며, 다른 체크아웃에는 없을 수 있다.
