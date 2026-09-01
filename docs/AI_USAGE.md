# AI 활용 내역

> 슈퍼드리머 8기 과제 — AI 도구 활용 범위·방법 (제출 시 명세서에 포함)

## 사용 도구

- **Claude Code** (Anthropic) — 모델 `claude-sonnet-5`. CLI 기반 코딩 어시스턴트.

## 원칙

- 과제 설계·판단·검증·통합은 **전부 본인**이 수행.
- AI는 (1) 요구사항을 코드 골격으로 옮기고, (2) 트레이드오프를 정리하고, (3) 버그 원인 후보를 좁히는 데 사용.
- AI가 생성한 모든 코드는 본인이 읽고 이해한 뒤 프로젝트에 반영하고, 에디터에서 동작을 확인.
- 게임 디자인(코어 루프, 지층·스킬·보스 규칙, 밸런스 수치, UI/흐름)은 본인이 정의하고 지시.

## 세부 내역

### 1. 일정 관리
- 마감 역산 스프린트 타임라인 초안을 요청, 진행 상황에 따라 매일 재조정을 지시.

### 2. 코드 스캐폴딩 (지시 → 골격 생성 → 본인 통합)
- 격자 맵: 행 스트리밍, `ObjectPool` 기반 블록/엔티티 풀링, 결정론적 해시 배치, `MapRow` 풀링, 편대 스폰, 보스 스폰
- 플레이어: 그리드 스텝 이동·채굴 통합, 낙하, 대시/충격파 코루틴, 보호막 버프, 토네이도 리프트
- 스탯/이코노미: `PlayerStats` 정적 API, `CurrencyCost` 값 타입, 강화 비용, PlayerPrefs 저장
- 스킬: `Skill` 베이스 + `SkillSystem` 자동 수집 + 버튼/단축키
- 무기: `SwordRarity` enum, `SwordIconSet` SO, `PlayerWeapon` 부채꼴 스윙 + 슬래시 이펙트
- 적/보스: `Enemy`, `GridEntity` 베이스, `Boss`/`Boss2` 텔레그래프-강타 패턴, 페이즈 시스템, `Fireball`, `DeathToss`
- 연출: `JuiceDirector`(히트스톱+Perlin 흔들림), `DamageNumbers`, `BossAttackVisual`(마커+충격파), `LowHealthVignette`, `TransientVfx`
- 카메라: `CameraFollow` 하강 추적 + 사망 되감기 + 흔들림
- 게임 흐름: `GameManager` 상태 머신, 사망 시퀀스, 타이틀/인벤토리 UI
- 오디오: `SfxPlayer` + `SfxBank` + `SfxHub`
- 배경: `ScrollingBackground` / `ScrollingBackgroundTiles`

### 3. 아키텍처 논의
- 씬 싱글톤 vs 인스펙터 주입, 이벤트 구독 시점(Awake vs Start), 보스 공격 패턴의 서브클래스 확장 방식, UI 갱신을 이벤트 구동으로 할지 등 트레이드오프를 정리받고 **최종 채택은 본인이 결정**.

### 4. 디버깅 (증상 설명 → 원인 후보·진단 로그 → 본인 검증)
- 보스 경고 마커 미표시(원인: 컴포넌트 미배치 / Start 타이밍)
- 무기 이펙트 미표시(원인: sorting layer, 알파, 스프라이트 할당)
- 대시 버튼 깜빡임(원인: `IsReady`가 이동 중 상태를 포함)
- 사망 튕김 미작동(원인: `_deathToss` 필드 미연결 → 자동 탐색으로 수정)
- 이펙트 각도 한 등급만 틀어짐(원인: 스프라이트 아트/피벗 → per-등급 보정 추가)
- 플레이어 멈춤(원인: 코루틴 플래그 stuck → try/finally)

### 5. 리팩토링
- 재화 표시를 컴포넌트별 → 배열 관리 하나로 통합(`CurrencyDisplay`)
- 프레임 비용 정리: `OnGUI` 빌드 제외, UI 값 변경 시에만 대입, 데미지 넘버 빌보드 제거
- `MapGenerator` 할당 억제: `MapRow` 풀링
- 보스 공격 시스템을 int ID + `protected virtual`로 확장 가능하게

### 6. 문서
- 본 문서 및 기술 명세서 초안 작성. 사실 확인·수정·최종본은 본인.

## 미사용

- 이미지 생성 AI 미사용 (아트는 Kenney CC0 팩).
- 사운드 생성 AI 미사용 (Kenney CC0 음원 + 효과음ラボ 무료 음원. 상세·라이선스는 기술명세서 §6).
