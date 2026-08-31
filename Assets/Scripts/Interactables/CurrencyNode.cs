using System;
using UnityEngine;
using Game.Map;
using Game.Economy;

namespace Game.Interactables
{
    /// <summary>
    /// 격자에 지층 대신 배치되는 재화 광맥. 부수면 지정한 재화 1종을 지급한다. 접촉 피해 없음.
    /// 배치·풀링은 <see cref="GridEntity"/> / <see cref="MapGenerator"/> 경로 (적·상자와 동일).
    /// 재화 종류별로 프리팹을 하나씩 만들어 스폰 테이블에 넣는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CurrencyNode : GridEntity
    {
        [Header("Currency Node")]
        [SerializeField] private int _maxHp = 25;
        [SerializeField] private CurrencyType _currency = CurrencyType.Coin;
        [SerializeField, Min(1)] private int _minAmount = 1;
        [SerializeField, Min(1)] private int _maxAmount = 3;
        [SerializeField] private SpriteRenderer _renderer;

        /// <summary>부순 결과: (재화 종류, 지급량). 토스트/사운드용.</summary>
        public event Action<CurrencyType, int> Collected;

        private int _hp;

        private void Reset()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
            Tint(1f);
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
                Tint(Mathf.Clamp01((float)_hp / _maxHp));
                return false;
            }

            int give = UnityEngine.Random.Range(_minAmount, Mathf.Max(_minAmount, _maxAmount) + 1);
            RunWallet.Instance?.Add(_currency, give);
            Collected?.Invoke(_currency, give);
            Debug.Log($"[CurrencyNode] {_currency.DisplayName()} +{give}", this);

            Map.ClearEntity(this);
            // TODO: 드롭 파티클 / 픽업 튐 연출 / 사운드
            return true;
        }

        private void Tint(float t)
        {
            if (_renderer == null)
            {
                return;
            }
            Color hi;
            switch (_currency)
            {
                case CurrencyType.Coin: hi = new Color(0.78f, 0.80f, 0.85f); break; // 은빛
                case CurrencyType.Gold: hi = new Color(0.95f, 0.80f, 0.28f); break; // 금빛
                case CurrencyType.Rune1: hi = new Color(0.55f, 0.80f, 1f); break;   // 파랑
                case CurrencyType.Rune2: hi = new Color(0.60f, 1f, 0.60f); break;   // 초록
                case CurrencyType.Rune3: hi = new Color(1f, 0.55f, 0.85f); break;   // 분홍
                default: hi = Color.white; break;
            }
            Color lo = hi * 0.5f;
            _renderer.color = new Color(
                Mathf.Lerp(lo.r, hi.r, t),
                Mathf.Lerp(lo.g, hi.g, t),
                Mathf.Lerp(lo.b, hi.b, t),
                1f);
        }
    }
}
