# Age of Dominion (에이지 오브 도미니언, 가칭)

채팅으로 동맹을 맺고 배신하며, 돌도끼 부족에서 우주 문명까지 영토를 넓히는 멀티플레이 전략 게임입니다.
Godot 4.6 · GDScript · 웹 브라우저 지원. 설계 원문은 `docs/design.md`, 작업 규약은 `AGENTS.md`, 진행 기록은 `PLAN.md`.

> 이전 프로젝트(3D RTS "Astragene")는 Git 태그 `astragene-rts-final`에 그대로 남아 있습니다.

## 바로 하기
- **`게임 실행.cmd`** 더블클릭 → 메뉴.
  - **혼자 시작**: 봇·AI 국가와 한 판(15~30분).
  - **방 만들기**: 내 PC가 호스트. 대기실에 나오는 IP를 친구에게 알려 주세요.
  - **접속**: 호스트의 IP 입력.
- **`전용 서버.cmd`**: 창 없이 서버만 실행(첫 접속자가 방장). 친구가 모두 들어올 때까지 PC를 켜 두세요.
- 게임 방법은 메뉴의 "게임 방법" 또는 게임 중 **F1**.

### 친구와 접속할 때
- 포트: 데스크톱 **27500/UDP**(ENet), 브라우저 **27501/TCP**(WebSocket). Windows 방화벽 허용 창이 뜨면 허용하세요.
- 같은 공유기(와이파이)면 대기실에 표시된 `192.168.x.x` 주소로 바로 접속됩니다.
- 인터넷 너머 친구는 공유기 **포트포워딩** 또는 **Tailscale** 같은 가상 LAN이 필요합니다(리슨 서버 방식의 한계).
- 연결이 끊겨도 같은 PC에서 같은 주소로 다시 접속하면 자기 나라로 돌아옵니다(재접속 토큰).

## SETUP
- 표준(비 .NET) **Godot 4.6.3**을 `.tools/godot-std/`에 두면 실행기가 사용합니다(`Godot_v4.6.3-stable_win64.exe`와 `_console.exe`).
  없으면 https://godotengine.org/download/archive/4.6.3-stable/ 에서 받습니다. PATH의 `godot`도 사용할 수 있습니다.
- 테스트 프레임워크: `./tools/setup_gdunit.ps1` (GdUnit4 6.2.1, MIT).

```powershell
. ./tools/use_local_tools.ps1
godot --headless --import
godot --headless -s addons/gdUnit4/bin/GdUnitCmdTool.gd --ignoreHeadlessMode -a tests/
godot --headless -s tools/sim_runner.gd -- --seed=42 --minutes=20
```

## 구조
| 폴더 | 내용 |
|---|---|
| `core/` | 결정론 시뮬레이션(정수만, `SimRng`만, Node 없음): 맵 생성, 인구·금, 영토 침투 전투, 상륙선, 건물, 8시대·특성, 조약·배신, 미사일·핵·해킹, 승리, 스냅샷 |
| `core/ai/` | 봇(확장 FSM), AI 국가(유틸리티 AI · 성향 4종 · 신뢰도 기억 · 조약 판단 · 배신) |
| `data/rules.json` | 모든 수치(정수). 밸런스는 여기서만 고칩니다 |
| `net/` | 리슨/전용 서버(100ms 턴 릴레이·해시 검사·스냅샷 복구·재접속·채팅 중계·욕설 필터·AI 대사), 클라이언트 |
| `presentation/` | 소유권 텍스처 + 셰이더 1회 드로우 지도, HUD, 채팅, 외교 팝업, 메뉴·대기실 |
| `tests/` | GdUnit4: 규칙·결정론·스냅샷·AI 풀 매치·ENet 루프백·재접속·8인 동기화 |

## 네트워크 방식과 보안 한계
- 서버 릴레이형 결정론적 락스텝: 클라이언트는 **의도(Intent)** 만 보내고, 서버가 100ms마다 묶어 모두에게 전달합니다. 모든 PC가 같은 시뮬레이션을 돌리고 50틱마다 해시를 비교해, 어긋나면 서버가 스냅샷을 보내 복구합니다.
- **모든 클라이언트가 전체 상태를 가지므로** 1:1 DM의 존재나 조약 제안이 다른 사람 화면에 안 보이는 것은 "표시상"의 비공개입니다. 채팅 **본문**은 서버가 수신자에게만 보냅니다.
- 브라우저 클라이언트는 `ws://`로 접속합니다. HTTPS로 올린 웹 페이지(itch.io 등)에서는 브라우저가 `ws://` 접속을 막으므로, 웹 빌드는 `http://`로 제공하거나 WSS 프록시가 필요합니다.
