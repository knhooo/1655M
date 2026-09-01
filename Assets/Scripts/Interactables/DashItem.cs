using UnityEngine;
using Game.Player;

namespace Game.Interactables
{
    /// <summary>
    /// ㄹ자 대시 아이템. 획득 즉시 좌우로 <b>벽(맵 끝 / 못 뚫는 지층)에 막힐 때까지</b> 돌진하고,
    /// 한 줄 내려간 뒤 반대 방향으로 다시 돌진하기를 <see cref="_passes"/> 회 반복한다.
    /// 지나는 지층·적은 관통 타격한다.
    /// </summary>
    public class DashItem : PickupEntity
    {
        [Header("ㄹ자 대시")]
        [Tooltip("좌우 왕복 횟수. 3 = 우→하→좌→하→우.")]
        [SerializeField, Min(1)] private int _passes = 3;
        [Tooltip("한 번 왕복하고 내려가는 줄 수.")]
        [SerializeField, Min(1)] private int _dropPerPass = 1;
        [SerializeField] private float _damageMultiplier = 2f;
        [SerializeField] private float _stepDuration = 0.045f;

        protected override string OnCollected(PlayerController pc)
        {
            pc.StartSerpentineDash(_passes, _dropPerPass, _damageMultiplier, _stepDuration);
            return "ㄹ DASH!";
        }
    }
}
