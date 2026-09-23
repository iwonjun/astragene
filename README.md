# 으후-브우부

Godot 4.6 .NET / C# .NET 8 기반 자체 IP RTS. 로컬 AI 대전, 직접 IP 락스텝 멀티플레이, 리플레이까지 플레이할 수 있습니다(Phase 0~13 완료).
규약 원문은 AGENTS.md, 로드맵과 검증 기록은 PLAN.md에 있습니다.

## SETUP

**가장 쉬운 실행:** 저장소 폴더의 `게임 실행.cmd`(게임) 또는 `에디터 열기.cmd`(에디터)를 더블클릭합니다.
두 실행기는 `.tools/`의 Godot 4.6.3 **.NET** 빌드만 사용하고, 실행 전에 C#을 빌드합니다.
일반(비 .NET) Godot로 이 프로젝트를 열면 "Mono 모듈을 지원하지 않습니다" 경고와 복구 모드 창이 뜹니다.
그 경우 경고 창에서 **취소**를 눌러 프로젝트가 수정되지 않게 하고 위 실행기로 다시 여세요.

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
Phase 5부터 GdUnit4 6.2.1(MIT)을 사용합니다. `./tools/setup_gdunit.ps1`로 설치합니다.

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
사용자 승인으로 Phase 1~13을 연속 진행하며 완료 조건을 충족한 순서로 커밋합니다.

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

## 맵 (Phase 2)
`godot --headless --script res://tools/gen_map.gd`로 duel.map을 생성합니다.
AGMP magic, 버전 1, 폭/높이 128, 이후 행 우선 순서로 flags(u8), height(u8), resource ID(i32)를 저장합니다.
모든 다중 바이트 값은 little-endian입니다. 잘못된 길이/버전/타일은 로더에서 거부합니다.
SpatialHash는 4타일 셀 후보 ID를 정렬 반환합니다. 정밀 거리 판정은 호출자가 수행합니다.


## 명령 및 로컬 선택 (Phase 5)
명령은 48바이트 고정 little-endian 형식이며 유닛별 대기 큐는 8칸입니다.
SimWorld는 명령 배열을 틱에 입력받고 읽기 스냅샷과 이벤트 큐를 제공합니다.
선택은 View에만 저장됩니다. 박스/동종/컨트롤 그룹, Ctrl 토글, 최대 200개 선택을 지원합니다.
씬 통합 검증: `godot --headless -s addons/gdUnit4/bin/GdUnitCmdTool.gd --ignoreHeadlessMode -a test/`.
GdUnit4 6.2.1은 기본적으로 headless를 거부하므로 플래그가 필요합니다.
테스트는 선택 함수를 직접 호출해 검증하며 OS 마우스 입력 전달은 headless 검증 대상이 아닙니다.
프레임워크: https://github.com/godot-gdunit-labs/gdUnit4/tree/v6.2.1 (설치본에 MIT LICENSE 포함).

## 현재 구현 (Phase 9)
Sim/전투/경제/시야와 절차 생성 3D 화면이 연결되었습니다. 실행은 `godot res://game/scenes/match.tscn` 입니다. 기본 boot/로비 화면은 다음 HUD 단계에서 연결합니다. WASD/화면 가장자리 이동, 휠 줌, 드래그/더블클릭/Ctrl 선택, 숫자 그룹, 우클릭 이동을 지원합니다. 실제 렌더 결과와 측정 조건은 docs/phase9-validation.md를 참고하세요.

## Phase 10 조작
- 기본 실행: `godot` (로컬 경기).
- 본진 선택 → Q: 일꾼 생산. 생산 대기열 항목 클릭: 취소와 전액 환불.
- 일꾼 선택 → 광물 우클릭: 채집. Z: 건설 메뉴, 건물 선택 후 지면 클릭. 우클릭/ESC: 배치 취소.
- 명령 버튼의 키 표시는 문맥에 따라 바뀝니다. Shift는 명령을 예약합니다.
- 미니맵 좌클릭/드래그: 카메라 이동, 우클릭: 선택 부대 이동.
- Enter: 채팅 입력, Tab: 전체/팀. 현재 로컬 기록이며 상대 전송은 네트워크 단계에서 연결합니다.
- ESC: 일시정지 메뉴, 단축키 편집 및 항복. 키 설정은 `user://hotkeys.json`에 저장합니다. WASD는 카메라 전용입니다.
- 폰트 라이선스와 고정 버전 출처: game/assets/fonts/. 화면: docs/phase10-hud.png.

## 튜토리얼 (처음 플레이하는 분)
- 로비 맨 위 **▶ 튜토리얼**. 또는 `godot -- --tutorial`.
- 카메라 → 일꾼 선택 → 광물 채집 → 일꾼 생산 → 보급(Beacon) → 병영(Drill Hall) → Trooper 4기 → 적 본진 공격 순서로 안내합니다.
- 각 단계는 실제로 해내면 자동으로 넘어가고, 목표 위치에 노란 링 표시, 눌러야 할 명령 카드는 반짝입니다. "건너뛰기"/"끝내기" 가능.
- 튜토리얼 밖의 경기에서는 시작 일꾼이 자동으로 광물을 캡니다. 명령 카드에 마우스를 올리면 설명이 나옵니다.

## 멀티플레이와 리플레이 (Phase 11)
- 로비: `게임 실행.cmd` → "방 만들기"(기본 포트 27415, UDP 방화벽 허용 필요) 또는 IP 입력 후 "접속". 진영 선택 → 준비 → 호스트가 "경기 시작".
- 네트워크로는 `CommandPacket { turn, playerId, Command[], hash }` 바이트만 오갑니다(ENet 단일 reliable 채널, RPC·동기화 노드 미사용). 로비 메시지도 turn -1 패킷의 Session 명령입니다.
- 1턴=2틱, 입력 지연 기본 3턴, RTT에 따라 2~6턴 자동 조정. 상대 패킷이 없으면 "플레이어 대기 중"을 표시하고 멈춥니다.
- 10틱마다 월드 해시를 대조하고 불일치 시 정지하며 `logs/desync_{tick}.json`에 각 프로세스의 상태를 기록합니다.
- 나간 플레이어의 유닛은 중립화되고 경기는 계속됩니다. 호스트가 나가면 경기가 중단됩니다(재접속 범위 밖).
- 리플레이는 `user://replays/`에 자동 저장됩니다. 로비의 "마지막 리플레이 보기"에서 1/2/4/8배속, 일시정지, 시점 전환이 가능하며 매 틱 해시를 검증합니다.
- 검증: `./tools/net_soak.ps1 -Minutes 15 [-DelayMs 200 -LossPercent 2]`. 결과는 docs/phase11-validation.md.

## AI (Phase 12)
- 로비의 "AI와 대전"과 난이도(쉬움/보통/어려움). AI는 Sim 안에서 돌아 멀티플레이·리플레이에서도 결정론이 유지됩니다.
- 구조: StrategyBrain(정찰 기억·위협·공격 시점·편성) → Economy/Production/ArmyManager → 점사·후퇴 마이크로.
- AI는 자기 VisibilityFilter로만 관찰하고 일반 Command만 제출합니다. 자원·시야 치트는 없습니다.

## 폴리싱 (Phase 13)
- 승패: 모든 건물을 잃거나 항복/탈퇴하면 패배, 마지막 남은 플레이어가 승리. 결과 화면에 채집량·생산량·처치/손실·분당 명령(APM) 그래프, "리플레이 보기".
- 오디오: Master/Music/SFX/Voice/UI 버스(`default_bus_layout.tres`). 진영별 선택·명령 응답 보이스, 전투 SFX, BGM 슬롯은 `tools/gen_audio.gd`가 합성한 플레이스홀더이며 같은 파일명으로 교체할 수 있습니다.
- 설정(로비 → 설정): 해상도, 전체 화면, 그래픽 품질, 볼륨 5종, 카메라 스크롤 속도/가장자리 스크롤, 명령 카드 단축키. `user://settings.json`.
- 방어탑 Sentinel/Thorn은 사거리 안의 적 유닛을 공격합니다(수치는 balance/rules.csv).
- 성능: 유닛 400 + 건물 100에서 틱 평균 0.95ms. docs/phase13-profile.md.

## 배포 빌드
1. Godot 4.6.3 **.NET** export 템플릿 설치: `에디터 열기.cmd` → 편집기 → 내보내기 템플릿 관리 → 다운로드 및 설치.
2. `./tools/build_release.ps1` (또는 `-Platform windows|linux|macos`). 결과는 `builds/`(Git 제외).
- macOS 빌드는 서명·공증을 하지 않습니다(`codesign/codesign=0`). 배포하려면 Apple 개발자 서명이 필요합니다.
