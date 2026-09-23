# Astragene 프로젝트 상태

작성일: 2026-09-23  
저장소: https://github.com/iwonjun/astragene  
작업 경로: `C:/Users/lee09/OneDrive/문서/으후-브우부`

## 완료된 작업

| 단계 | 상태 | 구현 내용 |
|---|---|---|
| Phase 0 | 완료 | Godot 4.6 .NET Forward+ 프로젝트, 순수 Sim 라이브러리, 씬 생성기, CI |
| Phase 1 | 완료 | Q32.32 고정소수점, 정수 삼각함수, 결정론 난수, 시계, 월드 해시 |
| Phase 2 | 완료 | 128×128 대칭 맵, 바이너리 포맷, SpatialHash, 절차 맵 생성 |
| Phase 3 | 완료 | SoA 엔티티 저장소, 세대형 EntityId, CSV 코드 생성 데이터 |
| Phase 4 | 완료 | FlowField, LRU 32 캐시, 공중 이동, 고정 반복 회피, 스톨 재계산 |
| Phase 5 | 완료 | 48바이트 명령, 큐, 상태머신, 선택·그룹·더블클릭 |
| Phase 6 | 완료 | 공격/방어 타입, 고지대, windup·쿨다운, 투사체·스플래시 |
| Phase 7 | 완료 | Ore/Plasma 채집, 건설, 생산 큐, 보급, 취소 환불, 업그레이드 |
| Phase 8 | 완료 | 시야 카운터, 탐색 유지, 차폐, 은폐·탐지, 유령 건물 |
| Phase 9 | 완료 | 10종 유닛·2진영 건물, 툰/림 셰이더, 지형·안개, MultiMesh, VFX, 카메라 |
| Phase 10 | 완료 | 자원 HUD, 미니맵, 선택·생산 패널, 명령 카드, 건설 미리보기, 채팅, 일시정지, 항복, 단축키 |
| Phase 11 | 완료 | ENet 락스텝, 로비·시드 합의, 대기 오버레이, 해시 검증·desync 덤프, 리플레이 |
| Phase 12 | 완료 | 결정론 AI 3단계(Easy/Medium/Hard), 계층형 두뇌·매니저·마이크로 |

## 검증 결과

- 순수 Sim 테스트 56/56 통과.
- Godot 통합 테스트 6/6 통과.
- Debug C# 빌드 경고 0개, 오류 0개.
- Phase 9 실제 GPU 측정: RTX 4060 Ti / Forward+ / 1600×900에서 400기 표시 구간 평균 0.9503ms, 최대 드로우콜 31회.
- 화면 결과: `docs/phase9-1.png`, `docs/phase9-2.png`, `docs/phase9-3.png`, `docs/phase10-hud.png`.
- Phase 9 측정 조건과 재현 명령은 `docs/phase9-validation.md`에 기록.
- 최신 완료 커밋: `14d3e8a phase-10: connect holographic HUD and command-driven controls`.

## 현재 진행 중

Phase 13 폴리싱. Phase 11 검증은 docs/phase11-validation.md(두 프로세스 15분, 무손실·200ms+2% 손실 모두 desync 0).

## 남은 작업

### Phase 13 — 폴리싱과 출시 빌드

- 보이스/SFX/BGM/오디오 버스, 승패·결과 화면, APM 그래프.
- 해상도·품질·볼륨·스크롤·단축키 설정.
- 상위 틱 시스템 계측, 500 엔티티 장기 프로파일링, Windows/Linux/macOS 빌드.

## 실행과 검증

```powershell
. ./tools/use_local_tools.ps1
dotnet test --configuration Release
godot --headless --script res://tools/build_ui.gd
godot --headless --script res://tools/build_scenes.gd
godot --headless -s addons/gdUnit4/bin/GdUnitCmdTool.gd --ignoreHeadlessMode -a test/
godot res://game/scenes/match.tscn
```

기본 조작은 WASD/화면 가장자리 카메라 이동, 휠 줌, 드래그·Ctrl 클릭·더블클릭·숫자 그룹 선택, 우클릭 문맥 명령, Enter 채팅, Esc 일시정지다.
