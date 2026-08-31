namespace Game.Equipment
{
    /// <summary>
    /// 무기는 "Sword" 한 종류, 5등급. 등급은 치명타 확률에만 영향.
    /// 등급별 치명타 확률 보너스: Normal +5 / Unique +10 / Legendary +15 /
    /// SuperLegendary +20 / UltimateLegendary +25 (%).
    /// </summary>
    public enum SwordRarity
    {
        Normal = 0,
        Unique = 1,
        Legendary = 2,
        SuperLegendary = 3,
        UltimateLegendary = 4,
    }

    public static class SwordRarityExtensions
    {
        /// <summary>이 등급 검이 올려주는 치명타 확률(%).</summary>
        public static float CritChanceBonus(this SwordRarity rarity)
        {
            return ((int)rarity + 1) * 5f;
        }

        public static string DisplayName(this SwordRarity rarity)
        {
            switch (rarity)
            {
                case SwordRarity.Normal: return "Normal";
                case SwordRarity.Unique: return "Unique";
                case SwordRarity.Legendary: return "Legendary";
                case SwordRarity.SuperLegendary: return "Super Legendary";
                case SwordRarity.UltimateLegendary: return "Ultimate Legendary";
                default: return rarity.ToString();
            }
        }
    }
}
