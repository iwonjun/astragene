# 작업 계획 — 에이지 오브 도미니언 Blitz

사용자 결정(2026-09-24): 기존 3D RTS를 폐기하고 기획서(docs/design.md)대로 처음부터 재작성.
저장소 main 교체(이전 상태는 태그 `astragene-rts-final`), GDScript + 웹, 리슨 서버 + 직접 IP, 목표 범위 Blitz 알파.

## Phase 0 — 기술 검증
### 변경 파일과 이유
- project.godot, .gitignore, .gitattributes, tools/use_local_tools.ps1, 실행기: 표준 Godot 4.6.3 GL Compatibility 2D 프로젝트.
- core/sim_rng.gd, core/map_gen.gd, core/sim.gd(상태·틱·증분 해시), core/intent.gd: 결정론 코어 골격.
- data/rules.json + core/ruleset.gd: 데이터 주도 수치 로드(정수).
- presentation/map_view.gd + territory.gdshader: 소유권 텍스처 렌더러(더티 타일만 갱신).
- net/protocol.gd, net/game_server.gd, net/game_client.gd: ENet/WebSocket 서버 릴레이, 턴 브로드캐스트, 해시 보고.
- tests/: 결정론·RNG·맵 생성·해시·루프백 네트워크 테스트.
### 완료 조건
- [x] 같은 시드와 같은 Intent 로그 → 1000틱 후 같은 해시(테스트).
- [x] 2개 프로세스가 ENet으로 접속해 타일 확장 시 해시 일치 (전용 서버 + 클라이언트 2, 3000틱, 해시 검사 100회+ 일치, 디싱크 0).
- [x] 400×240 맵 렌더 1 드로우(셰이더 스프라이트 1개), 틱 평균 1.3~1.6ms / 최악 ~30ms (예산 100ms).

## Phase 1 — Blitz MVP
인구·금·공격·건물 4종(도시/요새/시장/연구소), 시대 4개(구석기~중세), 스폰 선택, 전체+DM 채팅,
불가침·동맹 + 배신자, 확장 봇, 승리 판정, HUD/리더보드/메뉴·로비.
- [x] 20분 한 판이 끝까지 진행되고 승자가 결정된다: 헤드리스 AI 대전 4시드 10~20분 종료, 실제 창·브라우저에서 스폰→확장 확인.

## Phase 2 — Blitz 알파
8시대 전부와 특성, 국가 유틸리티 AI(성향·신뢰·외교), 조약 전체(휴전/무역/조공/기술 공유), 지도 핑,
해상 상륙·미사일·핵·해킹, 헤드리스 전용 서버, 재접속(스냅샷), 디싱크 복구, 웹 빌드(WebSocket), Windows 빌드.
- [x] 8인 + 봇 30 (+국가 6) 1500틱 동기화 테스트 디싱크 0, 재접속 토큰 복귀, 디싱크 시 스냅샷 복구.
- [x] 웹 빌드: 호스트 PC가 http://IP:27502 로 제공, 브라우저 클라이언트가 WebSocket으로 전용 서버 접속·경기 진행 확인.
- [x] Windows 빌드: builds/AgeOfDominion-windows.zip (web/ 포함).
### 남은 과제 (알파 이후)
- 실제 친구 4~8명 플레이테스트와 수치 조정(현재 수치는 AI 대전 기준). 인터넷 너머 접속은 포트포워딩/Tailscale 필요.
- Dominion(대전략) 모드, LLM 외교, Steam 연동은 범위 밖.
