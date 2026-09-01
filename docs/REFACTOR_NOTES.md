# 리팩토링 후보 (제출 후)

마감 전엔 **건드리지 말 것** — 보스/오디오 검증이 우선. 아래는 여유 생기면.

## 구조

1. **`Enemy` + `Boss` 공통 베이스 추출** — `CombatEntity : GridEntity`
   - 중복: `_hp/_maxHp`, `HealthChanged(int,int)` 이벤트, `Killed`, 접촉 피해 타이머, `Update`의 `IsPlayerAdjacent` 체크
   - 이점: `EnemyHpBar`를 보스와 공유 가능, 신규 적 추가가 쉬워짐
   - 리스크: 두 클래스 다 건드림 → 회귀 테스트 필요

2. **`MapGenerator` 분할** (~1000줄) — `partial class`로
   - `MapGenerator.Streaming.cs` / `.Strata.cs` / `.Entities.cs` / `.Formations.cs` / `.Boss.cs`
   - 동작 변화 없음, 순수 파일 분리

3. **`PlayerController` 슬림화** (~660줄)
   - 대시/충격파 코루틴이 여기 있음("배타 행동은 PlayerController가" 설계). 스킬 클래스로 옮기려면 공개 API 정리 필요 → 아키텍처 변경

## 사소

4. `Hash01` salt 매직넘버(1, 2, 7, 8 …) → 이름 붙은 상수/enum
5. `Boss.cs` 클래스 위 `<summary>` 주석 한 줄 중복 (예전 편집 흔적)
6. 사망 튕김이 두 방식 공존: `DeathToss`(적 컴포넌트) vs `BlockView.PlayDestroyToss`(인라인). 통일 고려
7. `TransientVfx`는 `Game.Enemies`에 있지만 범용 → `Game.Juice`로 이동이 적절
8. `Fireball` / 토네이도 VFX는 `Instantiate`/`Destroy` (풀링 없음). 수가 적어 지금은 OK
9. "어떤 이벤트가 어떤 사운드" 매핑이 `SfxHub` + 엔티티 코드에 분산 — 한 곳에 표로 문서화

## 성능 (프로파일링 후 판단)

10. 엔티티 `Update` 중앙화 — 현재 화면 ~15-25개라 무의미하나, 지형 유지로 동결 엔티티가 쌓이면 재검토
11. `PlayerWeapon` 스윙 코루틴 — 채굴이 잦아 `StartCoroutine` 반복 할당. Update 타이머로 전환 가능(GC 민감 시)
