using UnityEngine;
using Game.Player;

namespace Game.Interactables
{
    /// <summary>체력 회복 아이템. 인접 시 자동 획득 → 즉시 HP 회복.</summary>
    public class HealItem : PickupEntity
    {
        [Header("Heal")]
        [Tooltip("고정 회복량. _healPercent 가 0보다 크면 무시.")]
        [SerializeField] private int _healAmount = 200;
        [Tooltip("최대 HP 대비 회복 비율(0~1). 0이면 고정량 사용.")]
        [SerializeField, Range(0f, 1f)] private float _healPercent = 0f;

        protected override string OnCollected(PlayerController pc)
        {
            int amount = _healPercent > 0f
                ? Mathf.RoundToInt(pc.MaxHp * _healPercent)
                : _healAmount;
            pc.Heal(amount);
            return $"HP +{amount}";
        }
    }
}
