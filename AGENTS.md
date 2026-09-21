너는 Godot 4.6 기반 RTS 게임을 처음부터 구축하는 시니어 게임 엔지니어다.
아래 문서 전체가 이 프로젝트의 사양이자 작업 규약이다. 저장소 루트에 AGENTS.md 로
그대로 저장한 뒤, 매 세션 시작 시 다시 읽고 따른다.

================================================================
0. 프로젝트 개요
================================================================
장르: 스타크래프트 계열 실시간 전략 게임 (1v1 ~ 4인)
게임성/UI 관습: 90~2000년대 클래식 RTS 그대로 (자원 2종, 인구, 테크트리,
  생산 큐, 건설, 전장의 안개, 커맨드 카드, 컨트롤 그룹)
비주얼: 클래식 RTS의 무겁고 칙칙한 톤이 아니라 2020년대 모던 서브컬쳐
  (셀셰이딩 애니풍 + 홀로그래픽 UI). 상세는 5장 참조.
네트워크: 결정론적 락스텝 P2P (최대 4인) + 리플레이
IP: 100% 자체 IP. Blizzard 에셋/고유명사/정확한 밸런스 수치 사용 절대 금지.

================================================================
1. 기술 스택 (임의 변경 금지. 바꿔야 한다고 판단되면 먼저 질문할 것)
================================================================
- Godot 4.6 (.NET 빌드). 렌더러: Forward+
- 런타임 로직: C# (.NET 8)
- 시뮬레이션: Godot에 전혀 의존하지 않는 순수 C# 클래스 라이브러리
  (별도 .csproj, Godot 프로젝트가 ProjectReference로 참조)
- 툴링/에셋 생성: GDScript @tool 스크립트 + EditorPlugin
  (이유: `godot --headless --script` 로 CI에서 바로 실행 가능)
- 테스트: 순수 C# Sim은 xUnit + `dotnet test` (Godot 불필요),
  통합/씬 테스트는 GdUnit4
- 네트워킹: ENetMultiplayerPeer. 단, 6장의 제약을 반드시 지킬 것
- 셰이더: Godot Shading Language (.gdshader)

※ Godot 4.6의 실제 API가 아래 서술과 다르면 추측으로 우회하지 말고
  공식 문서를 확인한 뒤, 차이점과 대안을 보고하고 승인을 받아라.
  특히 CompositorEffect / RenderingDevice / MultiMesh 계열은 4.3~4.5에서
  시그니처가 여러 번 바뀌었으니 반드시 검증할 것.

================================================================
2. 절대 규칙 (위반 시 작업을 중단하고 사용자에게 보고)
================================================================
[R1] 시뮬레이션은 결정론적이어야 한다.
  - `src/Sim/` 이하에서 `using Godot;` 금지. 예외 없음.
    Vector2/Vector3, Mathf, GD.Randf, Time, Node, Resource 전부 금지.
  - float / double 사용 금지. 모든 수치는 Fix64 (Q32.32 고정소수점).
  - System.Random 금지. DetRandom(xorshift128+)만 사용.
  - Dictionary / HashSet 순회 결과에 로직이 의존하면 안 된다.
    순회가 필요하면 정렬 배열 또는 밀집 인덱스 배열을 쓴다.
  - DateTime.Now, 멀티스레드, Parallel.For, async 금지.
  - LINQ는 Sim에서 금지 (할당 + 순회 순서 불확실).

[R2] 레이어 의존은 단방향이다.
  View → Sim (읽기 전용). Sim → View 참조 금지.
  Sim이 외부에서 받는 것은 Command 뿐이고, 내보내는 것은 상태 조회와 이벤트 큐뿐이다.

[R3] 씬과 에셋은 코드로 생성한다.
  `tools/` 의 GDScript @tool 스크립트로 .tscn / .tres / .mesh 를 생성하며,
  `godot --headless --script res://tools/<name>.gd` 로 실행 가능해야 한다.
  "에디터에서 노드를 드래그해 연결하세요" 같은 지시를 남기지 마라.
  사람이 손으로 해야 하는 작업이 생기면 README.md 의 SETUP 섹션에 명시하고 최소화한다.

[R4] 테스트 없는 Sim 코드는 커밋하지 않는다.
  모든 Sim 시스템은 `tests/Sim.Tests/` 에 단위 테스트를 동반한다.

[R5] 한 번에 한 Phase만 진행한다.
  Phase 시작 전 `PLAN.md` 에 변경 파일 목록과 이유를 먼저 쓴다.
  Phase의 완료 조건(DoD)을 못 채우면 다음으로 넘어가지 않는다.
  Phase가 끝나면 작업을 멈추고 결과·미해결 이슈·다음 Phase 계획을 보고한 뒤 승인을 기다린다.
  커밋 단위는 Phase, 메시지는 `phase-N: 요약`.

[R6] 불확실하면 추측하지 말고 질문한다.
  특히 밸런스 수치, 네트워크 토폴로지, 아트 방향성은 임의 결정 금지.

================================================================
3. 저장소 구조 (변경 금지)
================================================================
/
  project.godot
  RtsGame.sln
  game/                      # Godot 프로젝트 루트 (res://)
    scenes/                  # boot.tscn, lobby.tscn, match.tscn, replay.tscn
    scripts/
      View/                  # 보간, MultiMesh 렌더링, 카메라, VFX
      UI/                    # HUD, 미니맵, 커맨드 카드, 로비
      Net/                   # 락스텝 러너, 턴 스케줄링, 해시 검증
      Bridge/                # Sim <-> Godot 어댑터 (여기서만 양쪽을 안다)
    shaders/                 # toon.gdshader, outline, fog, hologram_ui 등
    assets/generated/        # 코드로 생성된 메시/머티리얼/텍스처 (커밋함)
    maps/                    # .map 바이너리
  src/Sim/                   # 순수 C#. Godot 참조 0
    Core/                    # Fix64, Fix2, FixMath, DetRandom, SimClock, WorldHasher
    World/                   # Grid, Tile, MapData, SpatialHash
    Entities/                # EntityStore(SoA), EntityId, 컴포넌트
    Systems/                 # Movement, Combat, Economy, Production, Vision
    Pathing/                 # FlowField, FlowFieldCache, LocalAvoidance
    Commands/                # Command 구조체, CommandBuffer, 상태머신
    Data/                    # UnitDef, BuildingDef, DefDatabase (생성 코드)
    Ai/                      # 스킬미시 AI (Sim 안에 둔다. 이유는 Phase 12 참조)
  tests/Sim.Tests/           # xUnit
  tools/                     # GDScript @tool: 씬빌더, 메시생성, 맵생성, 밸런스임포터
  balance/                   # units.csv, buildings.csv, upgrades.csv (사람이 편집)
  .github/workflows/

================================================================
4. 게임 디자인 사양
================================================================
4.1 자원
  - Ore(광물 대응): 패치당 잔량 1500, 일꾼 1회 채집 5
  - Plasma(가스 대응): 간헐천, Extractor 건설 필요, 1회 4
  - Supply(인구): 사용/최대. 초과 시 생산 차단

4.2 진영 (자체 IP, 서브컬쳐 톤)
  [LUMINA 연합] — 기갑 슈트를 착용한 정규군. 정밀·질서·화이트/시안/골드.
    Engineer(일꾼) / Trooper(기본 원거리) / Lancer(대장갑 관통) /
    Howitzer(공성, 스플래시, 이동 중 공격 불가) / Wisp(공중 정찰, 고속)
    건물: Core(HQ) / Beacon(보급) / Drill Hall(병영) / Archive(테크) /
          Sentinel(방어탑) / Extractor
    특징: 일꾼이 건설 중 점유(건설 도중 이동 불가), 건물 수리 가능

  [VERGE 군체] — 유기적으로 증식하는 변이 생명체. 퍼플/마젠타/라임.
    Tiller(일꾼) / Render(근접) / Spitter(원거리) / Swarmling(저코스트 다수, 2기 동시 생산) /
    Wing(공중)
    건물: Nexus Pod(HQ) / Bloom(보급) / Hatchery(병영) / Gland(테크) /
          Thorn(방어탑) / Siphon
    특징: 자원만 소모하고 즉시 배치 후 자가 성장(일꾼 점유 없음), 건물 체력 자동 재생

  ※ 위 수치와 구성은 초안이다. 밸런스 수치는 balance/*.csv 에서 관리하고
    코드에 하드코딩하지 마라.

4.3 데미지 모델 (자체 테이블. 스타크래프트 수치 복제 금지)
  final = max(1, (damage * typeMul) - armor)
  방어 타입: Light / Medium / Heavy
  공격 타입별 typeMul:
    Normal    -> 1.00 / 1.00 / 1.00
    Piercing  -> 1.00 / 0.75 / 0.50
    Explosive -> 0.50 / 0.75 / 1.00
  고지대 보너스: 저지대 -> 고지대 공격 시 데미지 -25%
    (확률 기반 명중 회피는 결정론 유지를 위해 사용하지 않는다)

4.4 시뮬레이션 규격
  - 틱 20Hz 고정(50ms). 렌더는 가변, View에서 보간.
  - 락스텝 1턴 = 2틱(100ms), 입력 지연 기본 3턴(적응형 2~6).
  - 10틱마다 WorldHash(FNV-1a 64bit) 교환.
  - 성능 목표: 유닛 400 + 건물 100에서 틱당 5ms 이하, 렌더 60fps.

================================================================
5. 아트 디렉션 — "모던 서브컬쳐"
================================================================
핵심 명제: 게임플레이 가독성은 클래식 RTS 수준으로 유지하되,
표면 표현은 2020년대 애니풍 모바일/콘솔 게임의 감각을 따른다.
멀리서 봐도 유닛 종류·소속·상태가 0.3초 안에 구분되어야 한다.

5.1 셰이딩
  - toon.gdshader: NdotL을 2~3단계로 계단화(smoothstep, 하드 경계 아님).
    그림자색은 검정이 아니라 보색 계열 저채도(예: 보라빛 회색).
  - 림 라이트: 카메라 기준 프레넬, 진영 컬러로 착색. 실루엣 분리에 필수.
  - 아웃라인: 인버티드 헐(백페이스 확대) 방식을 기본으로 하고,
    거리에 따라 두께 보정(줌 아웃 시 너무 두꺼워지지 않게).
    포스트프로세스 엣지 검출은 4.6 API 확인 후 대안으로만.
  - 머티리얼은 전부 코드 생성(.tres), 유닛별 하드코딩 금지.

5.2 팀 컬러
  - 유닛 메시에 emissive 액센트 파트(바이저/코어/날개 끝)를 분리.
  - MultiMesh custom data 또는 인스턴스 유니폼으로 `team_color` 전달.
  - 팀 컬러는 알베도가 아니라 emissive에 실린다 → 어두운 지형에서도 즉시 구분.

5.3 유닛 디자인 (플레이스홀더도 이 규칙을 따른다)
  - 프로포션 1:4 ~ 1:5 (약간 데포르메). RTS 줌 거리에서 머리/실루엣이 보여야 함.
  - 역할별 실루엣 규칙:
      일꾼   = 작고 둥글다, 백팩 실루엣
      보병   = 슬림한 세로 실루엣, 어깨 아머로 폭 강조
      대장갑 = 긴 사선 무기(랜스/캐논)가 실루엣을 뚫고 나옴
      공성   = 낮고 넓은 실루엣 + 큰 포신, 전개 시 형태 변화
      공중   = 얇은 마름모, 지면에서 명확히 띄우고 그림자 데칼로 고도 표현
  - LUMINA는 직선/대칭/판넬 분할, VERGE는 곡선/비대칭/유기적 돌기.

5.4 UI — 홀로그래픽 + 글래스모피즘
  - 패널: 반투명 다크 베이스 + 얇은 네온 스트로크 + 모서리 45도 컷(각진 SF 프레임).
  - 배경 블러(BackBufferCopy 기반 셰이더), 아주 미세한 스캔라인/노이즈 오버레이.
  - 선택 유닛 초상화는 가챠 게임 카드 스타일 프레임(등급 테두리 대신 진영 컬러 테두리).
  - 리소스 숫자 변동 시 카운트업 트윈 + 증감 플로팅 텍스트.
  - 폰트: 한글 UI는 Pretendard 계열 등 오픈 라이선스 산세리프, 숫자는 별도 모노 계열.
  - 라이선스 확인 안 된 폰트/에셋은 절대 커밋하지 마라.

5.5 이펙트 (애니풍 임팩트)
  - 피격: 화이트 플래시 1~2프레임 + 방사형 링 스프라이트 + 짧은 히트스톱
    (히트스톱은 View 전용. Sim 틱은 절대 멈추지 않는다.)
  - 근접 공격: 반투명 슬래시 아크 메시, 진영 컬러 그라데이션.
  - 폭발: 애니메 특유의 "번짐 후 잔상" — 밝은 코어 + 링 충격파 + 짧은 크로마틱 애버레이션.
  - 유닛 생산 완료: 홀로그램이 실체화되는 디졸브 셰이더.
  - 블룸은 켜되 과하지 않게(threshold 1.0 이상). WorldEnvironment를 코드로 구성.
  - 모든 VFX는 풀링하고, Sim 상태에 어떤 영향도 주지 않는다.

5.6 지형
  - 절차적 하이트맵 → ArrayMesh. 3단계 스플랫.
  - 톤: 저채도 배경 + 고채도 유닛. 배경이 유닛보다 튀면 안 된다.
  - 고지대 경계는 명시적 클리프 메시 + 발광 라인으로 시각적 구분.

================================================================
6. 네트워킹 제약 (이 프로젝트의 핵심)
================================================================
- Godot의 MultiplayerSynchronizer / MultiplayerSpawner / @rpc 기반 상태 동기화를
  절대 사용하지 마라. 유닛 위치를 네트워크로 보내지 않는다.
- 주고받는 것은 오직 CommandPacket { turn, playerId, Command[], hash } 하나다.
  이 구조 덕분에 유닛 400기여도 대역폭은 초당 수 KB에 머문다.
- 전송은 ENetMultiplayerPeer의 단일 신뢰 채널 + 직접 바이트 직렬화.
- 호스트는 연결 중계와 턴 조정만 한다. 권위는 없다(모두가 같은 시뮬을 돌린다).
- Desync 검출 시 즉시 정지하고 `logs/desync_{tick}.json` 에 양측 상태 덤프 저장.
- 재접속은 범위 밖. 나간 플레이어의 유닛은 중립화하고 게임은 계속된다.
- 락스텝에서는 모든 클라이언트가 전체 월드를 시뮬레이션하므로 전장의 안개는
  "표시상의" 보안일 뿐이다. 이 한계를 README에 명시하라.

================================================================
7. 작업 로드맵 — Phase 0 ~ 13
================================================================
아래를 순서대로 진행한다. 각 Phase의 DoD를 충족하지 못하면 다음으로 넘어가지 않는다.

[Phase 0] 스캐폴딩
  project.godot(Forward+, .NET, 입력맵, 고정 틱), 솔루션/프로젝트 구조,
  Sim 라이브러리와 Godot 프로젝트의 ProjectReference 연결,
  tools/build_scenes.gd (boot/lobby/match/replay 빈 씬 생성),
  CI 워크플로(dotnet test + godot --headless 검증), PLAN.md, README.md
  DoD: `godot --headless --script res://tools/build_scenes.gd` 가 4개 씬 생성,
       `dotnet test` 통과, src/Sim 에서 `using Godot;` 쓰면 빌드 실패(의도 확인)

[Phase 1] 결정론 코어
  Fix64(Q32.32, long 128비트 중간연산으로 오버플로 방지), Fix2,
  FixMath(Sin/Cos/Atan2 — 1024엔트리 상수 룩업테이블을 코드로 생성해 커밋),
  DetRandom, SimClock, WorldHasher
  DoD: Sqrt가 0~10000 구간에서 double 대비 오차 0.001 미만,
       동일 시드 100만 회 수열 일치,
       기대값 하드코딩 골든 테스트가 x64/ARM에서 동일 통과

[Phase 2] 월드
  128x128 그리드(Walkable/Buildable/Height 0~2/ResourceNodeId),
  MapData 바이너리 직렬화, MapLoader, SpatialHash(셀 4타일, ID 정렬 순회),
  tools/gen_map.gd — 대칭 1v1 맵 절차 생성(본진+앞마당+중앙 개활지+고지대)
  DoD: 같은 맵 2회 로드 시 초기 WorldHash 동일

[Phase 3] 엔티티 & 데이터
  EntityStore(SoA, EntityId={index,generation} free list),
  컴포넌트(Transform/Health/Owner/UnitType/Movement/Combat/Cargo/Production/Vision),
  UnitDef·BuildingDef(plain struct), tools/import_balance.gd — CSV를 C# 상수 배열로 코드 생성
  (런타임 CSV 파싱 금지)
  DoD: 500 엔티티 생성/삭제 10만 회 후 메모리 안정 + 해시 결정론 유지

[Phase 4] 길찾기
  FlowField(정수 코스트, 직선10/대각14), LRU 캐시 32,
  결정론적 분리 스티어링 + push-apart 2회 고정 반복,
  스톨 감지(3초 무진전 → 경로 재계산), 공중 유닛은 직선 이동
  ※ 외부 RVO 라이브러리 사용 금지(float 기반, desync 원인)
  DoD: 동일 명령 시퀀스 2회 재생 시 5000틱 후 전 유닛 위치 해시 일치,
       미로 맵 도달률 100%, 200유닛 이동 시 틱당 3ms 이하

[Phase 5] 커맨드
  Command 구조체(고정 크기 직렬화), 타입: Move/AttackMove/Attack/Stop/Hold/
  Patrol/Follow/Build/Train/Cancel/Rally/Gather/Repair,
  유닛당 명령 큐 8, 명시적 상태머신 전이 테이블,
  View측: 드래그 박스 선택, 더블클릭 동종 선택, Ctrl+클릭, 컨트롤 그룹 0~9
  (같은 숫자 연타 시 카메라 점프), 선택 상한 200
  ※ 선택 상태는 클라이언트 로컬이다. Sim에 넣지 마라.

[Phase 6] 전투
  4.3 데미지 모델, 정수 쿨다운 + windup 분리,
  타겟 획득은 8틱마다 재평가(기존 타겟 > 나를 때리는 적 > 최근접),
  스플래시 3단계(100/50/25%, 아군 오사 있음), 투사체 엔티티, 즉사 처리(View가 연출)
  DoD: 동일 초기 상태 2군 전투 3000틱 후 생존자/체력 해시 일치

[Phase 7] 경제 & 생산
  일꾼 FSM(이동→채집40틱→반납), 노드당 동시 슬롯 2,
  진영별 건설 방식 차이(4.2 참조), 배치 검증(Buildable+충돌+테크+시야),
  생산 큐 5칸(취소 시 100% 환불), Supply, 테크트리, 업그레이드 3종×3단계
  DoD: 스크립트 봇이 일꾼12 → 병영 → 유닛생산까지 자동 수행하는 시나리오 테스트 통과

[Phase 8] 전장의 안개
  플레이어별 Unexplored/Explored/Visible, visionCount 정수 카운터 증감 방식
  (매 틱 전체 재계산 금지, 원형 스탬프 사전 계산),
  고지대 차폐, VisibilityFilter — View/UI/AI는 반드시 이 필터로만 적 정보를 읽는다,
  건물은 Explored 상태에서 마지막 스냅샷을 유령 표시, Cloak/Detector 훅만 준비
  DoD: 필터 우회 경로가 코드에 존재하지 않음(정적 검사 또는 테스트로 보장)

[Phase 9] 렌더링 & 아트 (5장 전면 적용)
  tools/gen_meshes.gd — 프리미티브 조합으로 5.3 규칙에 맞는 유닛 메시 절차 생성,
  toon/outline/dissolve/slash 셰이더, WorldEnvironment(블룸/톤매핑) 코드 구성,
  MultiMeshInstance3D 기반 렌더링(유닛마다 Node 하나씩 쓰지 마라),
  Sim 20Hz → 렌더 보간(위치 lerp, 회전 최단경로),
  선택 링 데칼, 빌보드 체력바(인스턴싱 쿼드, Control 노드 금지),
  카메라(고정 피치 ~50도, WASD/엣지스크롤/휠줌 고도20~60/미니맵 점프/스페이스=최근이벤트),
  지형 메시 + 스플랫, VFX 풀
  DoD: 400유닛 교전 60fps, 드로우콜 50 이하, 스크린샷 3장을 docs/ 에 저장

[Phase 10] HUD (5.4 스타일)
  상단 자원/인구(초과 시 경고 컬러), 좌하단 미니맵
  ※ 미니맵은 카메라 렌더텍스처가 아니라 Sim 데이터로 직접 그린다
    (지형 1회 베이크 + 유닛 점 + 시야 마스크 + 카메라 프러스텀 사각형),
  중앙하단 선택 패널(단일=초상화/스탯/생산큐, 다중=아이콘 그리드),
  우하단 3x4 커맨드 카드(컨텍스트별, 단축키 표기, 건설 서브메뉴),
  알림 시스템(자원 부족/인구 부족/본진 피격 → 미니맵 점멸+사운드+스페이스 점프),
  채팅(All/Team), 일시정지 메뉴, 항복, hotkeys.json 리매핑
  UI는 Sim을 읽기만 하고 입력은 Command로만 변환한다.

[Phase 11] 멀티플레이 (6장 제약 엄수)
  TurnManager(1턴=2틱, 지연 3턴, 적응형 2~6), CommandPacket 바이트 직렬화,
  전 플레이어 패킷 도착 전까지 시뮬 정지 + "플레이어 대기 중" 오버레이,
  10틱 해시 검증 및 desync 덤프, 로비(호스트/직접IP 조인/맵·진영 선택/준비/시드 합의),
  리플레이(CommandPacket 스트림+맵+시드 저장, 1/2/4/8배속, 일시정지, 시점 전환)
  DoD: 같은 머신 2개 빌드로 15분 대전 시 desync 0회,
       인위적 200ms 지연 + 2% 패킷로스에서 플레이 가능,
       리플레이 결과가 원본과 틱 단위로 완전 동일

[Phase 12] 스킬미시 AI
  계층 구조: StrategyBrain(빌드오더) → Economy/Production/ArmyManager → 유닛 마이크로.
  난이도 3단계(Easy: 반응 지연 큼 / Medium / Hard: 정찰 기반 대응. 자원 치트 없음).
  AI는 반드시 Command만 발행하고 Sim 내부 상태를 직접 수정하지 않는다.
  AI를 Sim 레이어에 두는 이유: 멀티플레이에 AI가 섞여도 결정론이 유지되어야 하기 때문.

[Phase 13] 폴리싱
  오디오(선택/명령 응답 보이스 플레이스홀더, 전투 SFX, BGM 슬롯, 버스 구성),
  승패 판정(전 건물 파괴 또는 항복), 결과 화면(채집량/생산량/APM 그래프),
  설정(해상도/품질/볼륨/스크롤속도/단축키), 
  프로파일링 패스(틱 시간 상위 5개 시스템 계측 후 최적화),
  Windows/Linux/macOS export preset 및 빌드 스크립트

================================================================
8. 검증 명령
================================================================
dotnet test                                              # Sim 단위 테스트
godot --headless --script res://tools/build_scenes.gd     # 씬 생성
godot --headless --script res://tools/gen_meshes.gd       # 에셋 생성
godot --headless -s addons/gdUnit4/bin/GdUnitCmdTool.gd -a test/   # 통합 테스트

================================================================
9. 지금 시작할 것
================================================================
이 문서를 AGENTS.md 로 저장하고, PLAN.md 에 Phase 0~13 로드맵을 기록한 뒤,
Phase 0만 수행하라. 완료되면 멈추고 보고하라.