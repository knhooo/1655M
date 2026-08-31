using System;
using UnityEngine;
using Game.Equipment;
using Game.Economy;
using Game.Flow;

namespace Game.Player
{
    /// <summary>
    /// 플레이어 유효 스탯 = 기본값 + Status 업그레이드(골드 소모) + 장착 검(치명타 확률).
    /// <see cref="PlayerPrefs"/> 로 영속. 런 시작 시 <see cref="PlayerController"/> 가 읽어 캐시한다.
    ///
    /// 기본값(임시): 대미지 17 · 최대 HP 500 · 치명타 확률 5% · 치명타 배수 2배.
    /// 검 등급별 치명타 확률: Normal +5 / Unique +10 / Legendary +15 / Super +20 / Ultimate +25.
    /// </summary>
    public static class PlayerStats
    {
        /// <summary>스탯이 바뀌었을 때 (강화·검 장착). UI 갱신용.</summary>
        public static event Action Changed;

        // ---- 기본값 ----
        public const float BaseDamage = 17f;
        public const int BaseMaxHp = 500;
        public const float BaseCritChance = 5f;   // %
        public const float CritMultiplier = 2f;

        // ---- Status 업그레이드 ----
        public const float DamagePerLevel = 2f;
        public const int HpPerLevel = 25;

        private const string DmgLevelKey = "status_dmg_lv";
        private const string HpLevelKey = "status_hp_lv";
        private const string EquippedSwordKey = "equipped_sword"; // -1 = 없음/자동

        public static int DamageLevel => Mathf.Max(0, PlayerPrefs.GetInt(DmgLevelKey, 0));
        public static int HpLevel => Mathf.Max(0, PlayerPrefs.GetInt(HpLevelKey, 0));

        // 다음 레벨 비용 (재화 2종 모두 소모). 레벨이 오를수록 증가.
        // 레벨 0→1: (1,1) / 1→2: (2,2) / ... (임시 곡선)
        public static CurrencyCost DamageUpgradeCost => new CurrencyCost(DamageLevel + 1, DamageLevel + 1);
        public static CurrencyCost HpUpgradeCost => new CurrencyCost(HpLevel + 1, HpLevel + 1);

        // ---- 계산된 스탯 ----
        public static int Damage => Mathf.RoundToInt(BaseDamage + DamageLevel * DamagePerLevel);
        public static int MaxHp => BaseMaxHp + HpLevel * HpPerLevel;
        public static float CritChance => Mathf.Clamp(BaseCritChance + EquippedSwordCritBonus(), 0f, 100f);

        // 강화 시 도달할 값 (UI 미리보기)
        public static int NextDamage => Mathf.RoundToInt(BaseDamage + (DamageLevel + 1) * DamagePerLevel);
        public static int NextMaxHp => BaseMaxHp + (HpLevel + 1) * HpPerLevel;

        // ---- 장착 검 ----
        /// <summary>장착 중인 검. 명시 안 됐거나 미보유면 보유 중 최고 등급 자동.</summary>
        public static SwordRarity? EquippedSword
        {
            get
            {
                int raw = PlayerPrefs.GetInt(EquippedSwordKey, -1);
                if (raw >= 0 && System.Enum.IsDefined(typeof(SwordRarity), raw))
                {
                    var r = (SwordRarity)raw;
                    if (IsOwned(r))
                    {
                        return r;
                    }
                }
                return HighestOwnedSword();
            }
        }

        public static float EquippedSwordCritBonus()
        {
            SwordRarity? s = EquippedSword;
            return s.HasValue ? s.Value.CritChanceBonus() : 0f;
        }

        public static void EquipSword(SwordRarity rarity)
        {
            if (!IsOwned(rarity))
            {
                return;
            }
            PlayerPrefs.SetInt(EquippedSwordKey, (int)rarity);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ---- 업그레이드 (재화 2종 모두 소모) ----
        public static bool CanAfford(CurrencyCost cost)
        {
            return GameManager.GetTotalCurrency(CurrencyType.Coin) >= cost.Coin
                && GameManager.GetTotalCurrency(CurrencyType.Gold) >= cost.Gold;
        }

        public static bool TryUpgradeDamage()
        {
            CurrencyCost cost = DamageUpgradeCost;
            if (!CanAfford(cost))
            {
                return false;
            }
            GameManager.AddTotalCurrency(CurrencyType.Coin, -cost.Coin);
            GameManager.AddTotalCurrency(CurrencyType.Gold, -cost.Gold);
            PlayerPrefs.SetInt(DmgLevelKey, DamageLevel + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static bool TryUpgradeHp()
        {
            CurrencyCost cost = HpUpgradeCost;
            if (!CanAfford(cost))
            {
                return false;
            }
            GameManager.AddTotalCurrency(CurrencyType.Coin, -cost.Coin);
            GameManager.AddTotalCurrency(CurrencyType.Gold, -cost.Gold);
            PlayerPrefs.SetInt(HpLevelKey, HpLevel + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // ---- 보유 검 조회 ----
        private static bool IsOwned(SwordRarity r)
        {
            return System.Array.IndexOf(RunInventory.GetOwnedSwords(), r) >= 0;
        }

        private static SwordRarity? HighestOwnedSword()
        {
            SwordRarity? best = null;
            foreach (SwordRarity r in RunInventory.GetOwnedSwords())
            {
                if (!best.HasValue || r > best.Value)
                {
                    best = r;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("1655M/Reset Progress (PlayerPrefs)")]
        private static void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[PlayerStats] 진행 데이터 초기화");
        }
#endif
    }
}
