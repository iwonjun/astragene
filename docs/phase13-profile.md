# Phase 13 프로파일링

측정: `ProfileTests.FourHundredUnitsAndHundredBuildingsStayWithinTickBudget` (Release, 로컬 x64 CPU).
시나리오: 두 진영 유닛 400기(역할 5종 혼합) + 보급 건물 100개, 서로 공격 이동. 시스템별 시간은 Sim이 표시만 하는
`ITickProfiler` 훅(Sim은 시계를 읽지 않음)을 테스트 쪽 Stopwatch로 구현해 측정했다. 순위는 엔티티 450개 이상 구간 기준.

| 순위 | 최적화 전 | ms/틱 | 최적화 후 | ms/틱 |
|---|---|---|---|---|
| 1 | Combat | 0.779 | Movement | 0.512 |
| 2 | Movement | 0.479 | Vision | 0.286 |
| 3 | Vision | 0.302 | Combat | 0.137 |
| 4 | Economy | 0.016 | Production | 0.009 |
| 5 | Orders/Queues | 0.015 | Economy | 0.003 |
| 전체 | 500 엔티티 평균 | **1.615** (최악 8.38) | 500 엔티티 평균 | **0.953** (최악 2.57) |

- 원인: 단일 대상 공격도 매 타격마다 전체 엔티티를 순회했고, 타겟 획득이 시야 밖 엔티티에도 시야 판정을 수행했다.
- 조치: 단일 대상 타격은 대상만 처리, 스플래시·타겟 획득은 축 방향 사각 범위로 먼저 걸러낸 뒤 정밀 판정. 결과는 동일(결정론·해시 테스트 통과).
- 예산 5ms/틱 대비 약 19%. 틱당 할당 94바이트.
- 장기 안정성: AI 대 AI 20분(24000틱) 동안 메모리 증가 8MB 미만 (`LongRunWithFiveHundredEntitiesIsStable`).
- 참고: 전체 테스트 스위트를 한 프로세스에서 JIT 예열 없이 처음 돌리면 첫 틱들이 더 느리므로, 측정 전 별도 월드로 예열한다.
