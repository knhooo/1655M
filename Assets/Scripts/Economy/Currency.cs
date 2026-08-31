namespace Game.Economy
{
    /// <summary>재화 2종. 대미지·체력 업그레이드에 둘 다 소모된다. (이름은 임시)</summary>
    public enum CurrencyType
    {
        Coin = 0,
        Gold = 1,
    }

    public static class CurrencyTypeExtensions
    {
        public const int Count = 2;

        public static string DisplayName(this CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Coin: return "Coin";
                case CurrencyType.Gold: return "Gold";
                default: return type.ToString();
            }
        }
    }

    /// <summary>업그레이드 비용 (재화 2종).</summary>
    public readonly struct CurrencyCost
    {
        public readonly int Coin;
        public readonly int Gold;

        public CurrencyCost(int coin, int gold)
        {
            Coin = coin;
            Gold = gold;
        }

        public int Get(CurrencyType type) => type == CurrencyType.Coin ? Coin : Gold;
    }
}
