using System.Collections.Generic;

namespace Game.Economy
{
    /// <summary>
    /// 재화 종류. (이름은 임시)
    ///  - Coin  : 모든 강화 공통
    ///  - Gold  : Damage / Armor 강화
    ///  - Rune1~3 : 각 스킬 1/2/3 강화 전용
    /// </summary>
    public enum CurrencyType
    {
        Coin = 0,
        Gold = 1,
        Rune1 = 2,
        Rune2 = 3,
        Rune3 = 4,
    }

    public static class CurrencyTypeExtensions
    {
        public const int Count = 5;

        public static string DisplayName(this CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Coin: return "Coin";
                case CurrencyType.Gold: return "Gold";
                case CurrencyType.Rune1: return "Rune 1";
                case CurrencyType.Rune2: return "Rune 2";
                case CurrencyType.Rune3: return "Rune 3";
                default: return type.ToString();
            }
        }
    }

    /// <summary>업그레이드 비용. 여러 재화 조합 가능 (현재는 Coin + 1종).</summary>
    public readonly struct CurrencyCost
    {
        private readonly int[] _amounts;

        public CurrencyCost(params (CurrencyType type, int amount)[] entries)
        {
            _amounts = new int[CurrencyTypeExtensions.Count];
            if (entries != null)
            {
                foreach ((CurrencyType type, int amount) e in entries)
                {
                    if (e.amount > 0)
                    {
                        _amounts[(int)e.type] += e.amount;
                    }
                }
            }
        }

        public int Get(CurrencyType type) => _amounts != null ? _amounts[(int)type] : 0;

        /// <summary>0 이 아닌 항목만.</summary>
        public IEnumerable<(CurrencyType type, int amount)> Entries()
        {
            if (_amounts == null)
            {
                yield break;
            }
            for (int i = 0; i < _amounts.Length; i++)
            {
                if (_amounts[i] > 0)
                {
                    yield return ((CurrencyType)i, _amounts[i]);
                }
            }
        }
    }
}
