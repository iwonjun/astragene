# 으후-브우부

Godot 4.6 .NET / C# .NET 8 기반 자체 IP RTS. Phase 0 스캐폴딩입니다. 4개 빈 씬이 준비되어 있으며 플레이 가능한 게임은 아직 없습니다.
규약 원문은 AGENTS.md, 로드맵과 검증 기록은 PLAN.md에 있습니다.

## SETUP

필수 도구: Godot 4.6 .NET (일반 빌드 아님), .NET 8 SDK, Git.
현재 PC에는 프로젝트의 `.tools/` 아래 Godot 4.6.3 .NET과 SDK 8.0.425가 준비되어 있습니다.
프로젝트 디렉터리에서 PowerShell을 열고 다음을 실행합니다. 환경 변경은 현재 셸에만 적용됩니다.

```powershell
. ./tools/use_local_tools.ps1
dotnet --version
godot --version
dotnet build RtsGame.sln
dotnet test
godot --headless --script res://tools/build_scenes.gd
godot --headless --editor --import
godot --headless --quit-after 3
./tools/verify_sim_boundary.ps1
```

`.tools/`는 Git에서 제외합니다. 다른 PC에서는 공식 배포본을 설치하여 `dotnet`, `godot` 명령을 PATH에서 사용할 수 있게 합니다.
- Godot .NET: https://godotengine.org/download/archive/4.6.3-stable/
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

사용자 승인에 따라 저장소 루트가 `res://`입니다. `game/`은 콘텐츠 디렉터리이며
씬 경로는 `res://game/scenes/`, 도구 경로는 `res://tools/`입니다.
AGENTS.md의 원문은 그대로 보존하고 경로 해석 승인은 PLAN.md에 기록했습니다.

Godot 에디터 열기: 위 셸에서 `godot --editor`를 실행합니다. 기본 실행 씬은 빈 Boot입니다.
현재 실행 중인 일반 Godot 대신 .NET 빌드를 사용해야 C# 기능을 사용할 수 있습니다.
도구 활성화 스크립트는 한글 SDK 경로 인식 문제를 피하기 위해
`%LOCALAPPDATA%/Codex/Toolchains/rts-dotnet-8`에 SDK 폴더 연결을 만듭니다.
프로젝트와 SDK의 실제 위치는 바꾸지 않습니다.

프로젝트 초기 설정을 재생성하려면 `godot --headless --script res://tools/configure_project.gd`를 실행합니다.
이 명령은 입력맵 기본값을 다시 설정하므로 키를 수정한 이후에는 의도적으로만 사용합니다.
카메라 WASD, 휠 줌, 좌클릭 선택, 우클릭 명령, Shift 큐, Ctrl 선택,
숫자 0~9 그룹, Space 최근 이벤트, Escape 메뉴의 입력 정의가 있습니다.
입력 처리와 게임플레이는 후속 Phase에서 구현합니다.
20Hz는 엔진 설정이며 순수 Sim의 시계는 Phase 1, 락스텝 구동은 Phase 11에서 구현합니다.

## 검증 및 CI

GitHub Actions는 Linux에서 Godot 없이 Sim 테스트/의존 차단을 확인하고,
Windows에서 전체 솔루션 빌드, 테스트, 씬 재생성 차이 검사 및 Godot headless 로드를 수행합니다.
원격 저장소는 https://github.com/iwonjun/astragene 입니다. Actions 실행 결과는 저장소의 Actions 탭에서 확인합니다.
현재 씬은 동작 없는 빈 노드이며 headless 검사는 로드/임포트 smoke check입니다.
실제 씬 동작의 통합 테스트는 GdUnit4로 추가하며 Phase 0에는 해당 플러그인을 설치하지 않습니다.

## 레이어

`src/Sim/`은 독립적인 .NET 8 라이브러리입니다. Godot 의존과 프로젝트/패키지 참조를 빌드에서 차단합니다.
소스의 Godot 토큰 검사에는 주석도 포함됩니다. 엔진 설명은 이 문서에 기록합니다.
SDK가 자동으로 추가하는 .NET 표준 어셈블리는 허용합니다.
Phase 0에는 게임 시스템을 구현하지 않으며 테스트는 어셈블리 경계와 대상 런타임을 검증합니다.
후속 Sim 구현은 고정소수점, 결정론적 순회, Command 입력과 상태/이벤트 출력 규칙을 따라야 합니다.
View는 Sim을 조회하고 Bridge를 통해 명령을 전달합니다.

## 네트워크 보안 한계

계획된 락스텝 방식에서는 모든 클라이언트가 전체 월드를 시뮬레이션합니다.
전장의 안개는 화면 표시상의 제한이며 악성 클라이언트의 전체 월드 접근을 방지하지 못합니다.
네트워크 및 게임플레이 구현은 해당 Phase에서 진행합니다.

## API 확인 자료

- https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_basics.html
- https://docs.godotengine.org/en/4.6/classes/class_projectsettings.html
- https://docs.godotengine.org/en/4.6/classes/class_packedscene.html
- https://docs.godotengine.org/en/4.6/classes/class_resourcesaver.html

- https://docs.godotengine.org/en/4.6/engine_details/file_formats/tscn.html

## 현재 남은 항목

로컬 검증은 통과했습니다. 에디터 headless 임포트 종료 시 `Scan thread aborted` 경고가 간헐적으로 나타나며
씬 로드 오류는 없습니다. Phase 0 구현과 로컬 검증을 완료했습니다.
Phase 1은 별도 승인 후 시작합니다.

## 결정론 코어 계약 (Phase 1)

Fix64는 signed Q32.32이며 곱셈/나눗셈은 Int128 중간값으로 계산하고 0 방향으로 절삭합니다.
결과 범위를 벗어나면 OverflowException, 0으로 나누면 DivideByZeroException입니다.
정수 바닥값 변환과 0 방향 변환을 구분합니다. 제곱근은 정수 연산만 사용해 아래쪽으로 절삭합니다.
각도 단위는 라디안입니다. Sin/Atan 각 1024개 테이블을 생성해 커밋하고 런타임에는 정수 보간만 합니다.
테이블 생성은 오프라인 도구에서만 부동소수점을 사용하며, 실행 플랫폼에서 재생성하지 않습니다.
Atan2(0,0)은 0입니다. 삼각함수는 보간 근사이며 코어 테스트가 정확도를 검증합니다.
난수는 SplitMix64 시드 확장 + xorshift128+이며 상태 두 개를 저장/복원합니다.
FNV-1a 해시는 정수의 little-endian 바이트를 사용하며 .NET 객체 해시를 네트워크 해시로 사용하지 않습니다.

사용자는 Phase 1~13 연속 진행과 시험용 밸런스 설계/조정을 승인했습니다.
단계별 완료 조건은 유지하며 추가 단계 승인 대기는 필요하지 않습니다.
