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
