namespace Game.Audio
{
    /// <summary>효과음 종류. <see cref="SfxBank"/> 에서 클립을 매핑한다.</summary>
    public enum SfxId
    {
        None = 0,

        Dig,          // 지층 채굴
        HitEnemy,     // 적/엔티티 타격 (비치명타)
        Crit,         // 치명타
        PlayerHurt,   // 플레이어 피격
        PlayerDeath,  // 플레이어 사망

        CoinPickup,   // 재화 획득
        ChestOpen,    // 상자 열기
        SwordGet,     // 무기 획득

        // 스킬 3종 — 슬롯 0/1/2 순서 유지 (SkillDash + slot 으로 접근)
        SkillDash,
        SkillShield,
        SkillShockwave,

        EnemyDeath,   // 일반 적 처치

        BossAttack,   // 보스 강타
        BossPhase,    // 보스 페이즈 전환
        BossDeath,    // 보스 처치

        UiClick,
        Upgrade,

        BossTelegraph,  // 보스 공격 경고 마커 등장
        BossShockwave,  // 보스 강타 충격파 (칸마다 1회)
        BossFireball,   // 보스2 파이어볼 발사
        BossTornado,    // 보스2 토네이도 발생
    }
}
