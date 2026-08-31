using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// 스킬 - 보호막. 일정 시간 동안 받는 피해를 감소시킨다.
    /// 실제 효과는 PlayerController.ActivateShield 가 처리. 강화 레벨로 지속·감소율 증가.
    /// (플레이어는 싱글톤 → 어느 오브젝트에 붙어도 됨)
    /// </summary>
    public class ShieldSkill : Skill
    {
        [Header("보호막")]
        [Tooltip("기본 지속 시간(초).")]
        [SerializeField, Min(0.1f)] private float _baseDuration = 4f;
        [Tooltip("레벨당 지속 시간 증가(초).")]
        [SerializeField, Min(0f)] private float _durationPerLevel = 0.5f;

        [Tooltip("기본 피해 감소율 (0~1). 0.5 = 50% 감소.")]
        [SerializeField, Range(0f, 1f)] private float _baseReduction = 0.5f;
        [Tooltip("레벨당 감소율 증가.")]
        [SerializeField, Range(0f, 0.2f)] private float _reductionPerLevel = 0.05f;
        [Tooltip("감소율 상한.")]
        [SerializeField, Range(0f, 1f)] private float _maxReduction = 0.9f;

        protected override bool CanActivate()
        {
            return Player != null && Player.IsAlive;
        }

        protected override void OnActivate()
        {
            int lv = UpgradeLevel;
            float duration = _baseDuration + lv * _durationPerLevel;
            float reduction = Mathf.Min(_maxReduction, _baseReduction + lv * _reductionPerLevel);
            Player.ActivateShield(duration, reduction);
        }
    }
}
