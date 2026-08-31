using System;
using UnityEngine;
using Game.Map;
using Game.Economy;
using Game.Equipment;

namespace Game.Interactables
{
    /// <summary>
    /// 격자에 지층 대신 배치되는 상자. 부수면 <b>재화 5종 중 하나 또는 랜덤 등급 검</b>을
    /// 가중치로 하나 골라 지급한다. 접촉 피해 없음.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Chest : GridEntity
    {
        private enum DropKind { Currency, Sword }

        [Serializable]
        private class Drop
        {
            public DropKind kind = DropKind.Currency;
            [Tooltip("kind == Currency 일 때 지급할 재화.")]
            public CurrencyType currency = CurrencyType.Coin;
            [Tooltip("kind == Currency 일 때 지급량(min~max).")]
            public Vector2Int amount = new Vector2Int(2, 5);
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        private class SwordChance
        {
            public SwordRarity rarity;
            [Min(0f)] public float weight = 1f;
        }

        [Header("Chest")]
        [SerializeField] private int _maxHp = 40;
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("이 중 가중치로 하나만 지급.")]
        [SerializeField]
        private Drop[] _drops =
        {
            new Drop { kind = DropKind.Currency, currency = CurrencyType.Coin,  amount = new Vector2Int(4, 10), weight = 35f },
            new Drop { kind = DropKind.Currency, currency = CurrencyType.Gold,  amount = new Vector2Int(2, 6),  weight = 20f },
            new Drop { kind = DropKind.Currency, currency = CurrencyType.Rune1, amount = new Vector2Int(1, 3),  weight = 10f },
            new Drop { kind = DropKind.Currency, currency = CurrencyType.Rune2, amount = new Vector2Int(1, 3),  weight = 10f },
            new Drop { kind = DropKind.Currency, currency = CurrencyType.Rune3, amount = new Vector2Int(1, 3),  weight = 10f },
            new Drop { kind = DropKind.Sword, weight = 15f },
        };

        [Header("검 등급 가중치 (kind == Sword 일 때)")]
        [SerializeField]
        private SwordChance[] _swordTable =
        {
            new SwordChance { rarity = SwordRarity.Normal, weight = 50f },
            new SwordChance { rarity = SwordRarity.Unique, weight = 30f },
            new SwordChance { rarity = SwordRarity.Legendary, weight = 14f },
            new SwordChance { rarity = SwordRarity.SuperLegendary, weight = 5f },
            new SwordChance { rarity = SwordRarity.UltimateLegendary, weight = 1f },
        };

        /// <summary>부순 결과 라벨. 토스트/사운드용. 예: "Coin +7", "Legendary 검".</summary>
        public event Action<string> Opened;

        private int _hp;

        private void Reset()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
            if (_renderer != null)
            {
                _renderer.color = new Color(0.72f, 0.52f, 0.26f);
            }
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
                if (_renderer != null)
                {
                    _renderer.color = new Color(0.55f, 0.40f, 0.20f); // 금 간 상자
                }
                return false;
            }

            GiveReward();
            Map.ClearEntity(this);
            return true;
        }

        private void GiveReward()
        {
            Drop d = PickWeighted(_drops, x => x?.weight ?? 0f);
            if (d == null)
            {
                Opened?.Invoke("빈 상자");
                return;
            }

            string label;
            if (d.kind == DropKind.Sword)
            {
                SwordRarity rarity = PickSword();
                RunInventory.Instance?.AddFound(rarity);
                label = $"{rarity.DisplayName()} 검";
            }
            else
            {
                int give = Roll(d.amount);
                RunWallet.Instance?.Add(d.currency, give);
                label = $"{d.currency.DisplayName()} +{give}";
            }

            Opened?.Invoke(label);
            Debug.Log($"[Chest] {label}", this);
            // TODO: 드롭 파티클 / 사운드
        }

        private SwordRarity PickSword()
        {
            SwordChance s = PickWeighted(_swordTable, x => x?.weight ?? 0f);
            return s != null ? s.rarity : SwordRarity.Normal;
        }

        // ------------------------------------------------------------------

        private static int Roll(Vector2Int range)
        {
            int lo = Mathf.Max(0, range.x);
            int hi = Mathf.Max(lo, range.y);
            return UnityEngine.Random.Range(lo, hi + 1);
        }

        private static T PickWeighted<T>(T[] items, Func<T, float> weight) where T : class
        {
            if (items == null || items.Length == 0)
            {
                return null;
            }

            float total = 0f;
            foreach (T it in items)
            {
                total += Mathf.Max(0f, weight(it));
            }
            if (total <= 0f)
            {
                return items[0];
            }

            float pick = UnityEngine.Random.value * total;
            float acc = 0f;
            foreach (T it in items)
            {
                acc += Mathf.Max(0f, weight(it));
                if (pick <= acc)
                {
                    return it;
                }
            }
            return items[items.Length - 1];
        }
    }
}
