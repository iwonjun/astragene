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
