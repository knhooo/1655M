using System;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;

namespace Game.Interactables
{
    /// <summary>
    /// 격자에 지층 대신 배치되는 재화 광맥. 공격하지 않아도 <b>플레이어가 닿으면(인접) 자동 수확</b>한다.
    /// (상자와 달리 부술 필요 없음.) 접촉 피해 없음.
    /// 배치·풀링은 <see cref="GridEntity"/> / <see cref="MapGenerator"/> 경로 (적·상자와 동일).
    /// 재화 종류별로 프리팹을 하나씩 만들어 스폰 테이블에 넣는다 (스프라이트는 프리팹에서).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CurrencyNode : GridEntity
    {
        [Header("Currency Node")]
        [SerializeField] private CurrencyType _currency = CurrencyType.Coin;
        [SerializeField, Min(1)] private int _minAmount = 1;
        [SerializeField, Min(1)] private int _maxAmount = 3;

        /// <summary>수확 결과: (재화 종류, 지급량). 토스트/사운드용.</summary>
        public event Action<CurrencyType, int> Collected;

        private bool _collected;

        protected override void OnPlaced()
        {
            _collected = false;
        }

        private void Update()
        {
            if (_collected || Map == null)
            {
                return;
            }

            PlayerController pc = Map.Player;
            if (pc == null || !pc.IsAlive)
            {
                return;
            }

            if (IsPlayerAdjacent(pc.Column, pc.Row))
            {
                Harvest();
            }
        }

        /// <summary>때려서도 수확 가능(닿기 전에 공격했을 때).</summary>
        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _collected)
            {
                return false;
            }
            Harvest();
            return true;
        }

        private void Harvest()
        {
            if (_collected)
            {
                return;
            }
            _collected = true;

            int give = UnityEngine.Random.Range(_minAmount, Mathf.Max(_minAmount, _maxAmount) + 1);
            RunWallet.Instance?.Add(_currency, give);
            Collected?.Invoke(_currency, give);
            Debug.Log($"[CurrencyNode] {_currency.DisplayName()} +{give}", this);

            Map.ClearEntity(this);
            // TODO: 드롭 파티클 / 사운드
        }
    }
}
