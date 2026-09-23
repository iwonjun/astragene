# 작업 계획

## 규약
AGENTS.md 원문을 매 세션 읽는다. 한 번에 한 Phase만 수행하며 DoD 충족 후 보고하고 다음 Phase 승인을 기다린다.

## 로드맵
- Phase 0: 스캐폴딩, 프로젝트 연결, 씬 생성, CI 및 검증.
- Phase 1: 결정론 코어와 골든 테스트.
- Phase 2: 월드, 맵 직렬화, 공간 인덱스.
- Phase 3: 엔티티 SoA 및 밸런스 데이터 생성.
- Phase 4: 결정론 길찾기 및 회피.
- Phase 5: 커맨드, 상태머신, 로컬 선택.
- Phase 6: 전투, 투사체, 데미지 모델.
- Phase 7: 경제, 건설, 생산, 테크.
- Phase 8: 전장의 안개 및 조회 필터.
- Phase 9: 렌더링, 절차 아트, 카메라, VFX.
- Phase 10: 홀로그래픽 HUD 및 입력.
- Phase 11: 락스텝 멀티플레이, 해시 검증, 리플레이.
- Phase 12: 결정론 스킬미시 AI.
- Phase 13: 오디오, 승패, 설정, 성능, 배포.

## Phase 0 — 완료
### 먼저 확인할 사항
- 사양은 루트 project.godot과 game/ = res://를 동시에 요구한다. 사용자 승인으로 저장소 루트 = res:// 확정. game/은 콘텐츠 디렉터리.
- .NET SDK 및 Godot .NET 실행 파일 확인. 시스템 PATH의 dotnet은 SDK 목록이 비어 있음.

### 변경 파일과 이유
- AGENTS.md: 사용자 사양 원문 보존.
- PLAN.md: 전체 로드맵과 현재 Phase 변경 계획 기록.
- README.md: SETUP, 생성/검증 명령, 레이어 규칙, 네트워크 보안 한계 기록.
- project.godot: Forward+, .NET, 입력맵, 20Hz 설정 및 시작 씬 연결.
- RtsGame.sln / RtsGame.csproj / global.json: .NET 8 프로젝트와 Godot 프로젝트 연결.
- src/Sim/Sim.csproj: Godot에 의존하지 않는 순수 시뮬레이션 라이브러리.
- tests/Sim.Tests/*: xUnit 경계 테스트; Phase 0에서는 게임 시스템 구현 없음.
- tools/build_scenes.gd: @tool 기반 boot/lobby/match/replay 빈 씬 생성.
- tools/verify_sim_boundary.ps1: Godot using 삽입 시 빌드 실패를 검증하고 임시 소스 제거.
- game/scenes/*.tscn: 생성된 4개 씬.
- .github/workflows/ci.yml: .NET 테스트, 금지 의존 검증, Godot headless 생성 및 프로젝트 검증.
- .gitignore / .gitkeep: 생성 캐시 제외 및 지정 빈 디렉터리 보존.

### 완료 조건
- [x] Godot 4.6.3 .NET으로 4개 씬 생성 성공.
- [x] dotnet test 성공(2/2).
- [x] Sim에 using Godot; 삽입 시 SIM002 빌드 실패 확인.
- [x] Godot .NET 프로젝트 빌드 및 4개 씬 headless 시작 확인.
- [x] 검증 결과와 미해결 사항 기록. Phase 0 단위 커밋으로 마무리하며 Phase 1은 승인 대기.

### 현재 검증 기록
- AGENTS.md는 첨부 파일과 바이트 단위 동일하게 복사.
- 프로젝트 전용 .tools/에 .NET SDK 8.0.425 및 Godot 4.6.3 .NET 설치 완료.
- src/Sim/Core/AssemblyInfo.cs: Sim 레이어의 어셈블리 메타데이터; 게임 로직 없음.
- tools/use_local_tools.ps1: 현재 셸에서만 로컬 도구를 활성화.
- dotnet test tests/Sim.Tests/Sim.Tests.csproj: 2/2 통과.
- tools/verify_sim_boundary.ps1: using Godot; 삽입 시 SIM002로 실패, 제거 후 정상 빌드(경고/오류 0).
- 위 초기 대기 사항은 해소되었으며 솔루션, 프로젝트 설정, 씬 생성 및 CI 작성 완료.
- 구현 DoD 충족. 사용자 제공 GitHub 계정명과 이메일로 저장소 로컬 작성자 설정 완료. Phase 1은 시작하지 않음.

### 승인 및 구현 보완
- 사용자 승인: 권장안대로 저장소 루트를 res://로 사용. game/은 콘텐츠 디렉터리이며 AGENTS.md 원문은 보존한다.
- tools/configure_project.gd 추가: Godot API로 입력맵과 Phase 0 프로젝트 설정을 생성.
- tools/build_scenes.gd 추가: 4개 빈 씬 생성, 저장 오류 확인.
- .gdignore 추가(src/, tests/, .tools/): 콘텐츠가 아닌 디렉터리의 에디터 임포트 제외.
- Git 저장소 초기화 및 origin을 https://github.com/iwonjun/astragene.git 으로 연결.


### 최종 로컬 검증
- dotnet build RtsGame.sln: 전체 3개 프로젝트 성공, 경고 0/오류 0.
- dotnet test: 2/2 통과.
- tools/verify_sim_boundary.ps1: 의도한 SIM002 실패 확인 후 파일 제거 및 정상 빌드 확인.
- Godot headless build_scenes.gd: boot/lobby/match/replay 생성.
- 생성기 2회 실행 전후 SHA-256 동일. 리소스 UID와 Godot 4.6 노드 unique_id를 이름 기반으로 생성.
- 4개 씬 각각 headless 3프레임 로드 성공, 기본 시작 씬 실행 성공.
- .NET 에디터 headless 임포트: 오류 없음. 종료 시 Scan thread aborted 경고가 간헐적으로 있음.
- 한글 SDK 경로 인식 문제: 실제 SDK는 유지하고 ASCII 경로 junction을 사용해 해결. tools/use_local_tools.ps1 및 README에 반영.
- CI 작성 완료. 원격 저장소/Actions 실행은 아직 없으므로 원격 성공을 주장하지 않음.
- GdUnit4는 실제 씬 동작 통합 테스트가 생기는 Phase에서 추가. 현재 검증은 빈 씬 로드 smoke check.
- Phase 0 커밋 메시지: `phase-0: scaffold Godot RTS project`. 원격 게시 및 CI 실행 결과는 GitHub Actions에서 확인.

### 다음 Phase 제안 (승인 전 실행 금지)
Phase 1에서 Fix64/Fix2/FixMath, DetRandom, SimClock, WorldHasher를 구현하고
정확도, 100만 난수열 재현, x64/ARM 골든 테스트를 추가한다.

## 사용자 추가 지시 — 연속 진행 승인
사용자는 Phase 0 이후 남은 모든 Phase를 단계별 재승인 없이 계속 진행하도록 지시했다.
이는 AGENTS.md 원문의 단계 종료 후 승인 대기 조항을 대체한다. 원문 파일은 보존한다.
한 번에 한 Phase, 사전 계획, 테스트, 완료 조건 및 Phase별 커밋은 유지한다.
불명확한 밸런스 수치 등 별도 설계 결정은 확인한다.

## Phase 1 — 진행 중
### 변경 파일과 이유
- src/Sim/Core/Fix64.cs, Fix2.cs: Q32.32 고정소수점, Int128 중간연산 및 벡터.
- src/Sim/Core/FixMath.cs, TrigTables.g.cs: 정수 제곱근 및 보간 삼각함수 LUT.
- tools/gen_trig_tables.gd: 1024개 Sin 및 Atan 상수 테이블 오프라인 생성.
- src/Sim/Core/DetRandom.cs: 명시적 상태의 xorshift128+와 SplitMix64 시드 확장.
- src/Sim/Core/SimClock.cs: 20Hz 정수 틱 시계.
- src/Sim/Core/WorldHasher.cs: 바이트 순서 명시 FNV-1a 64bit.
- tests/Sim.Tests/CoreTests.cs: 경계, 오버플로, 정확도, 고정 기대값 및 100만 수열 테스트.
- .github/workflows/ci.yml: x64/ARM64 동일 골든 테스트 실행.
- README.md: 산술 반올림/오버플로, 각도 단위, 해시 직렬화 계약 기록.
### 완료 조건
- [ ] 0~10000 제곱근 오차 <0.001.
- [ ] 동일 시드 100만 난수열 일치.
- [ ] 하드코딩 골든 테스트 x64/ARM64 모두 통과.
- [ ] 전체 빌드 및 의존 차단 검증, Phase 1 커밋.
- 추가 승인: 사양에 없는 시험용 밸런스 수치 설계와 플레이 테스트 조정을 에이전트에 위임함. balance/*.csv에서 관리한다.

### Phase 1 검증 중간 결과
- 로컬 Release 테스트 8/8 통과. 제곱근 0~10000을 0.01 간격으로 검증, 난수 100만 수열 일치.
- 고정 골든 값: Q32.32 산술, sqrt(2), 삼각함수 사분면, 시드 1 난수 초기 상태/출력, FNV hello.
- 기존 코어 경계 검증 통과. ARM64 실제 실행 결과 대기 중이며 아직 Phase 2로 진행하지 않음.

### Phase 1 완료
- 커밋 0750b51. GitHub 실행 35624408453에서 ubuntu-24.04(x64), ubuntu-24.04-arm(ARM64) 모두 성공.
- 제곱근 정확도, 100만 동일 시드 수열, 고정 기대값 골든 테스트 완료 조건 충족.
- 전체 로컬 빌드/테스트 통과. Phase 2 시작.

## Phase 2 — 진행 중
### 변경 파일과 이유
- src/Sim/World/Tile.cs, Grid.cs: 불변 128x128 타일, 유효 좌표와 높이/자원 검증.
- src/Sim/World/MapData.cs, MapLoader.cs: 버전 포함 little-endian 바이너리 직렬화 및 엄격한 로더.
- src/Sim/World/SpatialHash.cs: 4타일 셀, ID 정렬 조회, 이동/삭제 지원.
- tools/gen_map.gd: 180도 대칭의 1v1 맵, 본진/앞마당/중앙/고지대 및 자원 배치.
- game/maps/duel.map: 생성 결과 커밋.
- tests/Sim.Tests/WorldTests.cs: 두 번 로드 해시 일치, 왕복/손상 검증, 대칭/연결성, 공간 조회 순서.
### 완료 조건
- [ ] 생성 맵 2회 로드 초기 해시 일치.
- [ ] 맵 생성/직렬화/공간 조회 테스트 및 빌드 통과.

### Phase 2 완료
- Release 테스트 11/11 통과. 같은 맵 두 번 로드 및 직렬화 왕복 해시 동일.
- 180도 대칭, 양측 본진-앞마당-중앙 연결성, 높이 단계, 자원 ID 검증 통과.
- 공간 조회는 셀 후보를 ID 정렬로 반환하며 정밀 거리/범위 필터는 호출자 책임.

## Phase 3 — 진행 중
### 변경 파일과 이유
- src/Sim/Entities/EntityId.cs, EntityStore.cs, Components.cs: 세대 ID, 밀집 SoA, 고정 용량 free list, 상태 해시.
- src/Sim/Data/Definitions.cs, DefDatabase.g.cs: 유닛/건물/업그레이드 불변 정의와 생성 테이블.
- balance/units.csv, buildings.csv, upgrades.csv: 사용자 위임 시험용 밸런스.
- tools/import_balance.gd: CSV 스키마/ID/값 검증 후 정수 원시값 C# 생성.
- tests/Sim.Tests/EntityTests.cs: 슬롯 재사용, 오래된 ID 거부, 500 슬롯 10만 생성/삭제, 해시 및 할당량 검증.
### 완료 조건
- [ ] 500개 엔티티 생성/삭제 10만 회 후 메모리 안정과 해시 결정론 유지.
- [ ] CSV 생성 테이블 빌드 및 유닛/건물 정의 유효성 테스트 통과.

### Phase 3 완료
- Release 테스트 14/14 통과.
- 500개 슬롯 생성/삭제를 10만 배치(두 저장소에서 총 1억 회 생성)로 반복하고 고정 간격 상태 해시 일치 및 hot loop 할당 0바이트 확인.
- 세대 ID, 오래된 핸들 거부, 컴포넌트 초기화 및 읽기 스냅샷 격리 검증.
- 유닛 10종, 건물 12종, 업그레이드 9단계 CSV 코드 생성과 정의 참조 검증 완료.

## Phase 4 — 진행 중
### 변경 파일과 이유
- src/Sim/Pathing/FlowField.cs, FlowFieldCache.cs: 정수 10/14 코스트 Dijkstra, corner-cut 방지, 32개 LRU.
- src/Sim/Pathing/LocalAvoidance.cs: ID 순서, 고정 2회 push-apart, 충돌 검증.
- src/Sim/Systems/MovementSystem.cs: 지상 필드/공중 직선, 60틱 스톨 경로 재계산.
- src/Sim/Entities/Components.cs, EntityStore.cs: 이동 목표 활성/반경/진전 상태와 해시 확장.
- tests/Sim.Tests/PathingTests.cs: 미로 도달, 캐시 교체, 5000틱 결정론 및 200유닛 틱 3ms 검증.
### 완료 조건
- [ ] 동일 입력 5000틱 위치/상태 해시 일치.
- [ ] 미로 도달률 100%, 200유닛 이동 틱 평균 <3ms.

### Phase 4 완료
- Release 테스트 19/19 통과: 5000틱 재생 해시 동일, 미로 모든 보행 가능 타일 경로 존재, 실제 이동 8/8 도착.
- 200유닛/1000틱 평균 <3ms 통과(초기 flow field 생성 제외, 로컬 CPU 기준).
- 32개 LRU 교체, 벽 모서리 통과 금지, 공중 직선 이동 및 겹친 유닛 분리 검증.

## Phase 5 — 진행 중
### 변경 파일과 이유
- src/Sim/Commands/Command.cs, CommandQueue.cs, CommandStateMachine.cs: 48바이트 wire 명령, 유닛별 8칸 큐, 명시적 전이.
- src/Sim/World/SimWorld.cs: 외부 명령만 상태를 변경하는 시뮬 실행 API 및 조회/이벤트 경계.
- game/scripts/Bridge/MatchBridge.cs: Godot와 Sim 변환 및 명령 제출.
- game/scripts/View/SelectionController.cs: 로컬 선택, 박스/동종/Ctrl/0~9 그룹 및 카메라 점프 신호.
- tests/Sim.Tests/CommandTests.cs: 직렬화, 잘못된 입력, 큐/상태 전이, 선택 데이터 비포함 확인.
- tools/build_scenes.gd: match에 Bridge/SelectionController 노드 코드 생성.
- GdUnit4 및 통합 테스트 구성: 입력/씬 연결 확인.

### Phase 4 원격 성능 보완
- 원격 x64 평균 3.363ms, Windows Debug 11.1402ms로 기존 임계값 실패를 확인.
- 회피 후보 정렬 반복을 제거하고 ID 밀집 순서/축별 빠른 거부 후 필요한 쌍만 128비트 거리 연산.
- 성능 측정은 Release에서 실행하고 스트레스 테스트와 병렬 실행하지 않도록 변경.
- Phase 5 추가 진행 전 원격 재검증 필요.

### Phase 4 원격 재검증 완료
- ef2440f / Actions 35740287138: x64, ARM64 테스트와 Windows 전체 빌드/테스트/씬 검사 모두 성공.
- 최적화 후 로컬 Release 이동 평균 0.3445ms/틱.

### Phase 5 완료
- Sim 테스트 23/23, GdUnit4 씬/선택 통합 테스트 2/2 통과.
- 선택 한도 200, Ctrl 토글, 그룹 저장/복원/연타 점프 신호, 동종 선택 및 선택의 Sim 해시 비영향 확인.
- 최신 GdUnit4는 headless 실행을 기본 거부하므로 --ignoreHeadlessMode 사용. OS 이벤트 전달 대신 직접 선택/입력 메서드를 검증.
- 프레임워크 v6.2.1 MIT 라이선스 확인, tools/setup_gdunit.ps1로 고정 버전 설치, CI 포함.

## Phase 6 — 진행 중
### 변경 파일과 이유
- src/Sim/Systems/CombatSystem.cs: 타입/방어/고지대 데미지, 8틱 타겟 평가, windup/쿨다운, 투사체 저장소와 스플래시.
- src/Sim/World/SimWorld.cs: 공격 명령, 전투 틱, 즉시 사망 이벤트와 전투 상태 해시.
- src/Sim/Entities/Components.cs, EntityStore.cs: 최근 공격자 상태 및 해시.
- balance/units.csv, tools/import_balance.gd, src/Sim/Data/*: 투사체 속도/스플래시/이동 중 공격 정책 데이터.
- tests/Sim.Tests/CombatTests.cs: 데미지 테이블, 고지대, 아군 스플래시, 3000틱 생존자/체력 해시 동일.
### 완료 조건
- [ ] 동일 초기 2군 전투 3000틱 생존자/체력 해시 일치.

### Phase 6 완료
- Release 테스트 32/32 통과. 40유닛 두 진영 3000틱 전투의 생존자/체력/전체 상태 해시 일치.
- 방어 타입 표, 최소 피해 1, 고지대 25% 감소, 공성 windup/투사체 및 아군 스플래시 검증.
- 고지대/스플래시 배율은 방어력 차감 전에 적용하며 CSV 피해/사거리/속도를 사용.

## Phase 7 — 진행 중
### 변경 파일과 이유
- src/Sim/Systems/EconomySystem.cs: Ore/Plasma, 노드 2슬롯, 채집 40틱, 반납/수리/건설/재생.
- src/Sim/Systems/ProductionSystem.cs: 큐5, 취소환불, 인구, 테크 및 3종3단계 업그레이드.
- src/Sim/World/SimWorld.cs: 경제 초기 설정, 명령 라우팅, 리소스 상태/해시와 이벤트.
- balance/resources.csv, src/Sim/Data/*, tools/import_balance.gd: 자원/시작값 CSV 생성.
- src/Sim/Commands/Command.cs: Research 명령 추가.
- tests/Sim.Tests/EconomyTests.cs: 일꾼12->병영->유닛생산 시나리오 및 환불/슬롯/진영별 건설.
- Phase 8 VisibilityFilter 전에는 건설 시야 판정용 현재 아군 비전 원형 조회를 사용하고 이후 필터로 교체.

### Phase 7 검증 결과
- Release 경제 시나리오 5/5 통과. 초기 Ore 450에서 채집, 일꾼 12, 병영, 보급, Trooper 생산 수행.
- 생산 큐5, 취소100%환불, 두 진영 건설 차이, 노드2슬롯, 가스 추출기 필요, 업그레이드 순차 3단계 및 속도 반영 검증.
- 자원 규칙은 balance/rules.csv에서 코드 생성. 생산 큐가 인구를 예약하고 보급 상실 시 완성을 차단.

## Phase 8 — 진행 중
### 변경 파일과 이유
- src/Sim/Systems/VisionSystem.cs: 플레이어별 정수 visionCount/탐색여부, 원형 오프셋 사전계산, 이동/생성/삭제 때만 스탬프 갱신, 고지대 차폐.
- src/Sim/World/VisibilityFilter.cs: 플레이어별 조회 API, 적 상태 필터와 건물 마지막 스냅샷.
- src/Sim/World/SimWorld.cs: EntityStore 외부 접근 internal로 제한, 공개 필터 조회만 허용, 시야 상태 해시.
- game/scripts/Bridge/MatchBridge.cs, View/SelectionController.cs: 직접 EntityStore 조회 제거, VisibilityFilter로 전환.
- src/Sim/Entities/Components.cs: Cloaked/Detector 훅.
- tests/Sim.Tests/VisionTests.cs: 중첩 카운트, 탐색 유지, 고지대, 유령건물, 은폐/탐지, 공개 우회 API 정적 검사.

### Phase 8 완료
- Release Sim 테스트 41/41 및 Godot 통합 테스트 2/2 통과.
- 시야 변화가 없으면 스탬프 갱신 없음, 중첩 카운트 제거/탐색 유지, 고지대 차폐, 은폐/탐지 및 건물 마지막 스냅샷 검증.
- SimWorld의 EntityStore/상태/자원/이벤트 직접 조회를 internal로 제한하고 외부는 플레이어 결합 VisibilityFilter 사용.
- 보이지 않는 적 대상 명령 거부 및 Follow의 숨은 적 추적 차단.

## Phase 9 — 진행 중
### 변경 파일과 이유
- tools/gen_meshes.gd: 역할별 10개 유닛, 진영별 건물, 선택 링/체력바/VFX 메시와 재질 코드 생성.
- game/shaders/{toon,outline,dissolve,slash,terrain,fog,healthbar}.gdshader: 툰/림/아웃라인/홀로그램/전장의 안개 표현.
- game/scripts/View/{WorldRenderer,RtsCamera,VfxPool}.cs: MultiMesh, 위치/회전 보간, 풀링, 카메라 제어.
- game/scripts/Bridge/MatchBridge.cs: 실제 초기 경기/400유닛 부하 장면 설정, 표시 스냅샷과 성능 측정.
- tools/build_scenes.gd: 생성 지형, 조명/WorldEnvironment, 카메라/렌더러 씬 연결.
- docs/: 실 GPU 400유닛 렌더 성능 및 스크린샷 3장.
### 완료 조건
- [ ] 400유닛 교전 60fps, 드로우콜 50 이하 실측.
- [ ] 스크린샷 3장 저장, 씬/셰이더 검증 성공.

### Phase 9 성능 점검에 따른 추가 변경
- 근접한 개별 목표마다 대형 FlowField를 만드는 것이 교전 시작 지연의 원인. MovementSystem에 8타일 이내의 완전히 열린 사각 영역만 직선 이동하는 결정론적 단축 경로를 추가. 장애물/모서리 안전성과 다수 독립 목표 회귀 테스트를 동반한다.

### Phase 9 완료
- 10종 유닛/2진영 건물/자원 결정/3단 지형 생성, 툰·림·확대 헐, 디졸브, 시야 마스크, 풀링 VFX, 인스턴싱 체력바/선택 링, 카메라 연결.
- 400기 전체 표시 교전 421프레임 평균 0.9503ms, 최대 드로우콜 31. RTX4060Ti / Forward+ D3D12 / 1600×900, 짧은 구간의 로컬 측정. 전체 6초 측정과 구별해 docs/phase9-validation.md에 기록.
- docs/phase9-1.png(교전), phase9-2.png(역할별 갤러리), phase9-3.png(본진) 실제 GPU 캡처 육안 확인.
- Sim 42/42, GdUnit 3/3 통과, C# 빌드 경고/오류 0.
- [x] 400유닛 교전 60fps, 드로우콜 50 이하 실측.
- [x] 스크린샷 3장과 씬/셰이더 검증.

## Phase 10 — 진행 중
### 변경 파일과 이유
- tools/build_ui.gd, build_scenes.gd: HUD/부팅 메뉴와 글래스 패널/3×4 카드/미니맵/선택 패널을 코드로 생성.
- game/scripts/UI/{MatchHud,Minimap,Hotkeys}.cs: 필터 기반 표시, 컨텍스트 명령, 건설 메뉴, 생산큐, 알림, 로컬 채팅/일시정지/항복 입력.
- game/scripts/View/SelectionController.cs 및 Bridge/MatchBridge.cs: 지면 대상 명령과 우클릭 문맥, UI에 명령 전달. UI는 상태를 변경하지 않는다.
- game/shaders/hologram_ui.gdshader: 화면 블러, 잘린 모서리, 얇은 네온 테두리와 스캔라인.
- game/assets/fonts/: 라이선스 확인된 한글 산세리프/숫자 모노 폰트.
- game/hotkeys.json, test/hud_test.gd, tools/HudChecks.cs: 키 매핑 저장/로드와 HUD 선택·생산·채집 통합 테스트.
- 채팅의 실제 상대 전송은 Phase 11 CommandPacket 전송과 결합, 승패 결과 처리는 Phase 13에서 완성한다.

### Phase 10 완료
- 상단 자원 카운트업/증감, 직접 그리는 시야 기반 미니맵과 카메라 영역, 단일 초상화/스탯/취소 가능한 생산큐, 200명 스크롤 아이콘, 3×4 문맥 명령/건설 하위 메뉴 및 배치 윤곽 구현.
- 자원/보급 경고, 기지 피격 알림과 미니맵 점멸/Space 이동/음향, 전체·팀 채팅 입력 및 로컬 기록, 일시정지/항복 명령, hotkeys.json 리매핑 연결. 상대 채팅 전송은 Phase 11에서 결합.
- Pretendard / Roboto Mono는 각 공식 저장소 고정 커밋에서 내려받아 SIL OFL 1.1과 출처를 동봉. HUD 코드 생성 및 CI 재생성 검사에 포함.
- 생산 상세 조회는 소유자 필터를 통과해야 하며 복사 값만 반환. 항복은 직렬화되는 명령과 해시 상태로 추가; 승패/결과 화면은 Phase 13.
- Sim 44/44, GdUnit 4/4, 부팅/셰이더 검사 통과. 실제 화면 docs/phase10-hud.png 확인. 기본 실행은 이제 로컬 경기를 연다.

## Phase 11 — 진행 중
### 변경 파일과 이유
- src/Sim/Commands/{CommandPacket,TurnManager,ReplayLog}.cs: 고정 바이트 패킷, 정렬된 턴 버퍼/2틱 실행/해시 대조/리플레이 코어와 테스트.
- game/scripts/Net/{NetworkSession,LockstepRunner,ReplayController}.cs: ENet 단일 reliable 채널, 호스트 중계/멤버십/준비/시드, 대기 UI, RTT 기반 지연 조정, 재생.
- game/scripts/UI/Lobby.cs, tools/build_lobby.gd: 호스트/IP 접속/진영·맵·준비/시드/시작/리플레이 UI 생성.
- game/scripts/Bridge/MatchBridge.cs 및 UI/MatchHud.cs: 로컬/네트워크/리플레이 틱 경로, 채팅 및 일시정지 동기화.
- tools/net_soak.ps1 및 문서: 실제 두 Godot 프로세스 15분 교전, 200ms/2% 손실 전송 실험, 원본/리플레이 매틱 해시 비교 기록.
- src/Sim/World/SimWorld.cs: 결정론적인 탈퇴 중립화와 진단용 안정 상태 덤프. 게임 UI는 계속 VisibilityFilter만 사용.
### 완료 조건
- [x] 실제 2개 프로세스 15분, desync 0회.
- [x] 200ms 지연 + 2% 패킷 손실에서도 플레이, reliable 재전송 확인.
- [x] 리플레이 매틱 상태 해시가 원본과 동일.

### Phase 11 구현 보완 (현재 세션)
- src/Sim/Commands/LockstepSession.cs: 전송 계층과 무관한 락스텝 구동기. 턴마다 플레이어당 패킷 정확히 1개, 1턴=2틱, 지연 2~6턴을 각자 적응형으로 변경(증가 시 빈 턴 추가 송신, 감소 시 송신 생략). 턴 S 패킷에 턴 S-6 시작 시점 해시(5턴=10틱 체크포인트)를 실어 대조.
- src/Sim/Commands/SessionMessage.cs: 로비/제어 메시지를 turn -1 CommandPacket의 Session 명령에 담는 코덱(Hello/Assign/Lobby/SetFaction/SetReady/Start/Leave/Chat/Reject). 채팅은 24바이트 UTF-8 청크.
- src/Sim/Commands/ReplayLog.cs, ReplayPlayer.cs: 틱 단위 명령 스트림+틱별 해시, 맵/스폰/시드/콘텐츠 해시 포함. 되감기는 초기 상태부터 재시뮬레이션.
- src/Sim/World/SimWorld.cs: DumpState(desync 진단 JSON), 탈퇴 이벤트, 맵 해시 캐시. src/Sim/Systems/VisionSystem.cs: 시야 격자 해시를 증분 가중합으로 교체(틱별 해시 비용 절감). EconomySystem: 중립 소유자 업그레이드 조회 방어.
- game/scripts/Net/{NetworkSession,LockstepRunner,ReplayController,MatchLaunch,SoakBot}.cs: ENet 단일 reliable 채널과 PacketPeer 직접 바이트 전송(RPC/동기화 노드 미사용), 호스트 중계·좌석 배정·콘텐츠/맵 해시 확인·시드 합의, 대기 오버레이, RTT 기반 지연, desync 시 정지와 logs/desync_{tick}.json(프로세스별 섹션 병합), 리플레이 1/2/4/8배속·일시정지·시점 전환·매틱 검증.
- game/scripts/UI/Lobby.cs, tools/build_ui.gd(lobby_ui.tscn), tools/build_scenes.gd: 로비/리플레이 씬 코드 생성, boot → 로비.
- tools/udp_lossy_proxy.gd, tools/net_soak.ps1: 방향별 지연·손실 UDP 프록시와 두 프로세스 soak + 리플레이 검증 자동화.
- 한계: 호스트가 나가면 중계가 끊겨 경기가 중단된다(재접속·호스트 이전은 범위 밖). 동맹이 없으므로 팀 채팅은 본인에게만 표시. 멀티플레이 일시정지 메뉴는 경기를 멈추지 않는다.

## Phase 12 — 진행 중
### 변경 파일과 이유
- src/Sim/Ai/AiContext.cs: 난이도 프로필과 VisibilityFilter 기반 인지 스냅샷(아군/보이는 적/유령 건물/자원). 인덱스 순서 리스트만 사용.
- src/Sim/Ai/StrategyBrain.cs: 정찰 기억(방어 타입별 최대 관측 공급), 기지 위협 감지와 난이도별 반응 지연, 공격 목표, 편성 가중치(Hard는 관측 방어 타입에 맞춰 역상성 편성).
- src/Sim/Ai/EconomyManager.cs, ProductionManager.cs, ArmyManager.cs: 일꾼 분배/가스, 우선순위 빌드오더와 예산 보류, 부지 탐색과 실패 부지 기억, 집결/방어/공격 웨이브, 점사·후퇴 마이크로, Hard 일꾼 정찰.
- src/Sim/Ai/AiPlayer.cs, src/Sim/World/SimWorld.cs: AI 좌석을 월드 생성 인자로 받고 틱마다 필터 조회 후 명령을 일반 Accept 경로로 제출. AI 상태를 월드 해시에 포함.
- src/Sim/World/VisibilityFilter.cs: 자기 유닛 명령 상태 조회(OwnState)만 추가. src/Sim/Commands/ReplayLog.cs: AI 좌석 저장(버전 3).
- game/scripts/Bridge/MatchBridge.cs, UI/Lobby.cs: 로컬 대전에 선택 난이도 AI 좌석 연결.
- tests/Sim.Tests/AiTests.cs: 난이도별 빌드오더·공격, Hard>Easy 양 진영, AI 대 AI 결정론/리플레이, 필터 외 접근 부재와 자원 치트 부재.
### Phase 11 완료
- 두 프로세스 15분: 무손실/200ms+2% 손실 모두 desync 0, 체크포인트 1800회 대조, 리플레이 양쪽 18000틱 일치. 상세 docs/phase11-validation.md.
- Sim 테스트 49개(네트워크 5개 추가), GdUnit 6개(ENet 루프백 로비·채팅·락스텝·탈퇴, 로비/리플레이 씬) 통과.

### 완료 조건
- [x] 모든 난이도가 빈 상대를 상대로 일꾼/병영/병력 생산 후 공격, Hard는 15분 내 전멸.
- [x] Hard가 두 진영 조합에서 Easy를 이긴다(LUMINA 379초, VERGE 401초에 Easy 전 건물 파괴).
- [x] AI 포함 경기가 동일 입력에서 해시 일치, 리플레이 매틱 일치.
- [x] AI 코드가 SimWorld/EntityStore를 보유·참조하지 않고 Command만 발행.

### Phase 12 완료
- Sim 56/56, GdUnit 6/6 통과. 로비의 "AI와 대전"에서 쉬움/보통/어려움 선택, `godot -- --local --ai=2`로 직접 실행.
- 난이도 차이는 판단 주기(40/20/10틱), 위협 반응 지연, 목표 일꾼·병영 수, 점사/후퇴/정찰/역상성, 업그레이드 단계뿐이며 자원·시야 보정은 없다.
- 알려진 한계: 확장 기지를 짓지 않는다. 방어탑은 현재 전투 시스템이 건물 공격을 지원하지 않아 AI도 짓지 않는다(Phase 13 후보).


## Phase 13 — 진행 중
### 변경 파일과 이유
- src/Sim/World/SimWorld.cs, MatchStats.cs, VisibilityFilter.cs: 승패 판정(전 건물 파괴/항복/탈퇴), 채집·생산·손실·처치·분당 명령(APM) 통계, 경기 종료 후 공개 조회. 해시 포함.
- src/Sim/World/TickProfiler.cs: 시스템별 계측 훅(Sim은 시간을 읽지 않고 구간 표시만). tests/Sim.Tests/ProfileTests.cs: 500 엔티티 장기 계측, 상위 5개 시스템 기록과 틱 예산 검증 → docs/phase13-profile.md.
- tools/gen_audio.gd, game/assets/generated/audio/*, default_bus_layout.tres: 보이스/전투 SFX/BGM 플레이스홀더와 Master/Music/SFX/Voice/UI 버스 코드 생성. game/scripts/View/AudioDirector.cs: 선택·명령 응답, 전투음, BGM 슬롯.
- tools/build_ui.gd, game/scripts/UI/{Results,ApmGraph,SettingsMenu,GameSettings}.cs: 결과 화면(채집량/생산량/APM 그래프), 설정(해상도/전체화면/품질/볼륨/스크롤 속도/단축키) 저장·적용.
- export_presets.cfg, tools/build_release.ps1: Windows/Linux/macOS 내보내기 프리셋과 빌드 스크립트.
### 완료 조건
- [x] 승패가 결정론적으로 판정되고 결과 화면에 통계·APM 그래프가 표시된다(docs/phase13-results.png).
- [x] 상위 5개 시스템 계측 후 최적화, 유닛 400 + 건물 100에서 틱 평균 0.95ms(docs/phase13-profile.md).
- [x] 오디오 버스/보이스/SFX/BGM, 설정 저장·적용, 3개 플랫폼 프리셋과 빌드 스크립트.

### Phase 13 완료
- Sim 62/62, GdUnit 10/10, 1분 두 프로세스 soak desync 0·리플레이 일치.
- 추가: 방어탑(Sentinel/Thorn) 공격 구현(balance/rules.csv의 TowerDamage/Range/Cooldown), 보통·어려움 AI가 1개 건설.
- 배포: export_presets.cfg 3종은 Godot가 인식하며 누락 항목은 export 템플릿뿐이다. 템플릿(4.6.3 .NET) 설치 후 `./tools/build_release.ps1`로 빌드한다. 이 PC에는 템플릿이 없어 실제 실행 파일은 아직 만들지 않았다.
- 오디오는 코드로 합성한 플레이스홀더다(외부 샘플 없음). 실제 보이스/음악은 같은 파일명으로 교체하면 된다.
