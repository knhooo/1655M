using System;
using UnityEngine;
using Game.Map;
using Game.Player;

namespace Game.Enemies
{
    /// <summary>
    /// 격자에 박혀 있는 정지형 적. 이동하지 않고, 파괴될 때까지 진로를 막는다.
    /// 플레이어가 인접하면 일정 간격으로 접촉 피해를 준다. (무적시간은 플레이어가 관리)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Enemy : GridEntity
    {
        [Header("Enemy")]
        [Tooltip("임시 밸런스: 플레이어 대미지 17 기준. 45 ≈ 3타.")]
        [SerializeField] private int _maxHp = 45;
        [Tooltip("플레이어 최대 HP 500 기준. 접촉 1회 피해.")]
        [SerializeField] private int _contactDamage = 20;
        [Tooltip("접촉 피해 재적용 간격(초).")]
        [SerializeField] private float _contactInterval = 0.8f;
        [SerializeField] private SpriteRenderer _renderer;

        [Header("사망 보상 (임시)")]
        [SerializeField] private int _goldReward = 1;

        /// <summary>사망 시: (앵커 col, 앵커 row, 골드 보상). 드롭/점수 처리용.</summary>
        public event Action<int, int, int> Killed;

        private int _hp;
        private float _contactCooldown;

        private void Reset()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
            _contactCooldown = 0f;
            RefreshVisual();
        }

        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _hp <= 0)
            {
                return false;
            }

            _hp -= amount;
            RefreshVisual();
            // TODO: 히트 플래시 / 넉백 불가 표시 / 사운드

            if (_hp <= 0)
            {
                Killed?.Invoke(AnchorCol, AnchorRow, _goldReward);
                Map.ClearEntity(this); // 풋프린트 정리 + 풀 반납
                return true;
            }
            return false;
        }

        private void Update()
        {
            if (Map == null)
            {
                return;
            }

            if (_contactCooldown > 0f)
            {
                _contactCooldown -= Time.deltaTime;
                return;
            }

            PlayerController pc = Map.Player;
            if (pc == null || !pc.IsAlive)
            {
                return;
            }

            if (IsPlayerAdjacent(pc.Column, pc.Row))
            {
                pc.Damage(_contactDamage, transform.position);
                _contactCooldown = _contactInterval;
            }
        }

        private bool IsPlayerAdjacent(int pcol, int prow)
        {
            Vector2Int s = Size;
            for (int dx = 0; dx < s.x; dx++)
            {
                for (int dy = 0; dy < s.y; dy++)
                {
                    int c = AnchorCol + dx;
                    int r = AnchorRow + dy;
                    int manhattan = Mathf.Abs(c - pcol) + Mathf.Abs(r - prow);
                    if (manhattan <= 1) // 인접(상하좌우) 또는 겹침
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void RefreshVisual()
        {
            if (_renderer == null)
            {
                return;
            }
            float t = _maxHp > 0 ? Mathf.Clamp01((float)_hp / _maxHp) : 1f;
            // 남은 HP 낮을수록 어둡게. 임시 색(붉은 계열).
            Color hi = new Color(0.85f, 0.25f, 0.22f);
            Color lo = new Color(0.35f, 0.10f, 0.10f);
            _renderer.color = Color.Lerp(lo, hi, t);
        }
    }
}
