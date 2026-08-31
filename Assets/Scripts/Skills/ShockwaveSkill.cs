using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// 스킬 - 충격파. 플레이어 중심 원형 범위의 지층·적에 즉발 피해 (지형도 뚫음).
    /// 실제 판정은 PlayerController.Shockwave 가 처리. 강화 레벨로 반경·피해 증가.
    /// </summary>
    public class ShockwaveSkill : Skill
    {
        [Header("충격파")]
        [Tooltip("기본 반경(칸).")]
        [SerializeField, Min(1)] private int _baseRadius = 2;
        [Tooltip("이만큼 레벨마다 반경 +1.")]
        [SerializeField, Min(1)] private int _levelsPerRadius = 2;

        [Tooltip("타격 피해 배수 (기본 대미지 대비).")]
        [SerializeField, Min(0.1f)] private float _baseDamageMultiplier = 1.5f;
        [Tooltip("레벨당 피해 배수 증가.")]
        [SerializeField, Min(0f)] private float _damageMultiplierPerLevel = 0.1f;

        protected override bool CanActivate()
        {
            return Player != null && Player.IsAlive;
        }

        protected override void OnActivate()
        {
            int lv = UpgradeLevel;
            int radius = _baseRadius + lv / Mathf.Max(1, _levelsPerRadius);
            float mul = _baseDamageMultiplier + lv * _damageMultiplierPerLevel;
            Player.Shockwave(radius, mul);
        }
    }
}
