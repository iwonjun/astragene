# Phase 11 네트워크 검증

측정일 2026-09-23, 같은 Windows PC에서 Godot 4.6.3 .NET headless 프로세스 2개(호스트 P1 LUMINA, 클라이언트 P2 VERGE).
두 프로세스 모두 `SoakBot`이 VisibilityFilter만 읽고 Command를 발행한다(생산·채집·이동·공격 이동).
재현: `./tools/net_soak.ps1 -Minutes 15` / `./tools/net_soak.ps1 -Minutes 15 -DelayMs 200 -LossPercent 2`

| 조건 | 틱 | 실제 시간 | 해시 검증 체크포인트 | desync | 대기 프레임 | 입력 지연 | RTT | 리플레이 |
|---|---|---|---|---|---|---|---|---|
| 무손실 루프백 | 18000 (15분) | 903.5초 | 1800 / 1800 | 0 | 0 | 3턴 | 53ms | 양쪽 18000틱 전부 일치 |
| 방향별 200ms 지연 + 2% 손실 | 18000 (15분) | 931.8초 | 1801 / 1801 | 0 | 537 (3.0%) | 6턴 (자동) | 493ms | 양쪽 18000틱 전부 일치 |

- 손실 조건은 `tools/udp_lossy_proxy.gd`가 UDP 데이터그램을 직접 드롭한다(실측 드롭률 1.96%). ENet reliable 채널 재전송으로 명령 유실 없이 진행했고, 15분 경기가 약 28초 늘어났다.
- 양측 최종 월드 해시: 무손실 `d5be0049bc159a44`, 손실 `3c30308b712f542d` (각 조건에서 두 프로세스 동일).
- 리플레이는 각 프로세스가 기록한 파일을 새 프로세스(`replay.tscn -- --verify`)에서 재시뮬레이션해 모든 틱 해시를 비교했다.
- 같은 조건의 결정론 단위 테스트: `NetworkTests.FifteenMinuteLockstepUnderLatencyAndLossNeverDesyncsAndReplaysExactly` (지연 변경 3→6→2 포함), 인위적 분기 시 10틱 체크포인트 검출과 덤프 검증.
- 한계: 호스트 이탈 시 중계가 끊겨 경기가 중단된다. 전장의 안개는 표시상의 보안이다(README 참고).
