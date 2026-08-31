using UnityEngine;
using Game.Player;

namespace Game.Skills
{
    /// <summary>
    /// 스킬 1 - 돌진.
    /// 현재 위치에서 수직 아래 방향으로 빠르게 이동하며, 평소보다 넓은 범위로 지층/적을 타격한다.
    /// 실제 이동·타격은 <see cref="PlayerController.StartDash"/> 가 처리.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DashSkill : Skill
    {
        [Header("돌진")]
        [Tooltip("아래로 최대 몇 칸 돌진할지.")]
        [SerializeField, Min(1)] private int _distance = 5;

        [Tooltip("돌진 중 타격 피해 배수 (기본 대미지 대비).")]
        [SerializeField, Min(0.1f)] private float _damageMultiplier = 2f;

        [Tooltip("돌진 중 좌우로 추가 타격하는 범위(칸). 1 = 양옆 1칸씩 = 총 3칸 폭.")]
        [SerializeField, Min(0)] private int _widthRadius = 1;

        [Tooltip("돌진 한 칸당 이동 시간(초). 작을수록 빠름.")]
        [SerializeField, Min(0.01f)] private float _stepDuration = 0.03f;

        private PlayerController _player;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        protected override bool CanActivate()
        {
            return _player != null && _player.CanDash();
        }

        protected override void OnActivate()
        {
            _player.StartDash(_distance, _damageMultiplier, _widthRadius, _stepDuration);
        }
    }
}
