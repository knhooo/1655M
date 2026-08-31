using System;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;

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

        [Header("사망 보상 (임시)")]
        [Tooltip("처치 시 지급할 Coin (min~max).")]
        [SerializeField, Min(0)] private int _coinRewardMin = 1;
        [SerializeField, Min(0)] private int _coinRewardMax = 2;

        /// <summary>사망 시: (앵커 col, 앵커 row, 지급한 Coin). 점수/연출용.</summary>
        public event Action<int, int, int> Killed;

        /// <summary>HP 가 바뀔 때: (현재 HP, 최대 HP). HP 바 등 표시용. 배치 시 최대치로도 발생.</summary>
        public event Action<int, int> HealthChanged;

        public int MaxHp => Mathf.Max(1, _maxHp);

        private int _hp;
        private float _contactCooldown;

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
            _contactCooldown = 0f;
            HealthChanged?.Invoke(_hp, MaxHp);
        }

        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _hp <= 0)
            {
                return false;
            }

            _hp -= amount;
            HealthChanged?.Invoke(Mathf.Max(0, _hp), MaxHp);
            // TODO: 히트 플래시 / 넉백 불가 표시 / 사운드

            if (_hp <= 0)
            {
                int coin = UnityEngine.Random.Range(_coinRewardMin, Mathf.Max(_coinRewardMin, _coinRewardMax) + 1);
                RunWallet.Instance?.Add(CurrencyType.Coin, coin);
                Killed?.Invoke(AnchorCol, AnchorRow, coin);
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
    }
}
