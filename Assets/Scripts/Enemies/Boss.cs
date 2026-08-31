using System;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 뼈대. 격자에 <b>9x9</b> 로 박혀 제자리에서 공격. 파괴될 때까지 진로를 완전히 막는다(가로 전폭).
    /// 플레이어가 인접(또는 겹침)해 있으면 <see cref="_contactInterval"/> 초마다 1회 접촉 피해.
    /// 공격 패턴은 <see cref="PerformAttack"/> 를 채워서 구현 (지금은 로그만).
    ///
    /// 프리팹: GridEntity 의 Size 를 (9,9) 로 설정. 스폰은 <see cref="MapGenerator"/> 의 _bossPrefab/_bossRow 경로.
    /// </summary>
    public class Boss : GridEntity
    {
        [Header("Boss")]
        [SerializeField] private int _maxHp = 3000;
        [Tooltip("붙어 있을 때 1회 접촉 피해.")]
        [SerializeField] private int _contactDamage = 40;
        [Tooltip("접촉 피해 재적용 간격(초).")]
        [SerializeField] private float _contactInterval = 1f;

        [Header("공격 (뼈대)")]
        [Tooltip("공격 시도 간격(초).")]
        [SerializeField] private float _attackInterval = 3f;
        [Tooltip("등장 후 첫 공격까지 대기(초).")]
        [SerializeField] private float _attackWindup = 2.5f;

        [Header("사망 보상")]
        [SerializeField, Min(0)] private int _coinReward = 150;

        /// <summary>HP 변화: (현재, 최대). 보스 HP 바용. 배치 시 최대치로도 발생.</summary>
        public event Action<int, int> HealthChanged;
        /// <summary>처치: (앵커 col, 앵커 row, 지급 coin).</summary>
        public event Action<int, int, int> Killed;
        /// <summary>공격 발동 순간 (연출 훅).</summary>
        public event Action Attacking;

        public int MaxHp => Mathf.Max(1, _maxHp);
        public int Hp => _hp;
        public bool IsAlive => _hp > 0;

        private int _hp;
        private float _contactCd;
        private float _attackCd;

        protected override void OnPlaced()
        {
            _hp = MaxHp;
            _contactCd = 0f;
            _attackCd = Mathf.Max(0f, _attackWindup);
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

            if (_hp <= 0)
            {
                RunWallet.Instance?.Add(CurrencyType.Coin, _coinReward);
                Killed?.Invoke(AnchorCol, AnchorRow, _coinReward);
                Map.ClearEntity(this);
                // TODO: 클리어 연출 / 하강 재개
                return true;
            }
            return false;
        }

        private void Update()
        {
            if (Map == null || _hp <= 0)
            {
                return;
            }

            PlayerController pc = Map.Player;
            bool playerOk = pc != null && pc.IsAlive;

            // 접촉 피해 (붙어 있으면 1초에 1회)
            if (_contactCd > 0f)
            {
                _contactCd -= Time.deltaTime;
            }
            else if (playerOk && IsPlayerAdjacent(pc.Column, pc.Row))
            {
                pc.Damage(_contactDamage, pc.transform.position);
                _contactCd = _contactInterval;
            }

            // 공격 패턴 (뼈대)
            _attackCd -= Time.deltaTime;
            if (_attackCd <= 0f)
            {
                _attackCd = Mathf.Max(0.1f, _attackInterval);
                if (playerOk)
                {
                    PerformAttack(pc);
                }
            }
        }

        /// <summary>공격 1회. 여기에 패턴을 채운다 (열 붕괴 강타 / 지연 폭발 / 소환 등).</summary>
        protected virtual void PerformAttack(PlayerController player)
        {
            Attacking?.Invoke();
            Debug.Log("[Boss] PerformAttack (스텁)", this);
        }
    }
}
