using System;
using UnityEngine;
using Game.Map;
using Game.Economy;

namespace Game.Interactables
{
    /// <summary>
    /// 격자에 지층 대신 배치되는 재화 광맥. 부수면 지정한 재화 1종을 지급한다. 접촉 피해 없음.
    /// 배치·풀링은 <see cref="GridEntity"/> / <see cref="MapGenerator"/> 경로 (적·상자와 동일).
    /// 재화 종류별로 프리팹을 하나씩 만들어 스폰 테이블에 넣는다 (스프라이트는 프리팹에서).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CurrencyNode : GridEntity
    {
        [Header("Currency Node")]
        [SerializeField] private int _maxHp = 25;
        [SerializeField] private CurrencyType _currency = CurrencyType.Coin;
        [SerializeField, Min(1)] private int _minAmount = 1;
        [SerializeField, Min(1)] private int _maxAmount = 3;

        /// <summary>부순 결과: (재화 종류, 지급량). 토스트/사운드용.</summary>
        public event Action<CurrencyType, int> Collected;

        private int _hp;

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
        }

        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _hp <= 0)
            {
                return false;
            }

            _hp -= amount;
            if (_hp > 0)
            {
                // TODO: 크랙 스프라이트 / 히트 플래시
                return false;
            }

            int give = UnityEngine.Random.Range(_minAmount, Mathf.Max(_minAmount, _maxAmount) + 1);
            RunWallet.Instance?.Add(_currency, give);
            Collected?.Invoke(_currency, give);
            Debug.Log($"[CurrencyNode] {_currency.DisplayName()} +{give}", this);

            Map.ClearEntity(this);
            // TODO: 드롭 파티클 / 사운드
            return true;
        }
    }
}
