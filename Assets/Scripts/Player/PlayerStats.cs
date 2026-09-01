using System;
using UnityEngine;
using Game.Equipment;
using Game.Economy;
using Game.Flow;
using Game.Audio;

namespace Game.Player
{
    /// <summary>강화 가능한 항목.</summary>
    public enum UpgradeKind
    {
        Damage = 0,
        Armor = 1,   // 최대 HP
        Skill1 = 2,
        Skill2 = 3,
        Skill3 = 4,
        ItemHeal = 5,  // 체력 회복 아이템 효과량
        ItemDash = 6,  // ㄹ자 대시 아이템 위력
    }

    /// <summary>
    /// 플레이어 유효 스탯 = 기본값 + 강화(Coin·Gold 소모) + 장착 검(치명타 확률).
    /// <see cref="PlayerPrefs"/> 로 영속. 런 시작 시 <see cref="PlayerController"/> 가 읽어 캐시한다.
    ///
    /// 강화는 항목별 Lv 0 부터 시작(추가치 없음), 무한, 비용 선형(레벨당 Coin/Gold +1).
    /// 기본값: 대미지 17 · 최대 HP 1000 · 치명타 확률 5% · 치명타 배수 2배.
    /// </summary>
    public static class PlayerStats
    {
        /// <summary>스탯이 바뀌었을 때 (강화·검 장착). UI 갱신용.</summary>
        public static event Action Changed;

        // ---- 기본값 ----
        public const float BaseDamage = 17f;
        public const int BaseMaxHp = 1000;
        public const float BaseCritChance = 5f;   // %
        public const float CritMultiplier = 2f;

        public const float DamagePerLevel = 2f;
        public const int HpPerLevel = 25;

        // 아이템 강화 (레벨당 효과 증가치)
        public const int HealItemPerLevel = 60;      // 체력 회복 +60/레벨
        public const int DashItemLevelsPerPass = 2;  // 2레벨마다 대시 왕복 +1
        public const float DashItemDamagePerLevel = 0.35f; // 대시 관통 피해 배수 +0.35/레벨

        private const string EquippedSwordKey = "equipped_sword"; // -1 = 없음/자동

        // ------------------------------------------------------------------
        // 강화 (통합 API)
        // ------------------------------------------------------------------

        private static string LevelKey(UpgradeKind k) => "upg_lv_" + (int)k;

        public static int GetLevel(UpgradeKind k) => Mathf.Max(0, PlayerPrefs.GetInt(LevelKey(k), 0));

        /// <summary>
        /// 다음 레벨 비용. (임시: 선형)
        ///  - Damage / Armor : Coin + Gold
        ///  - Skill1~3       : Coin + 해당 스킬 전용 재화(Rune1~3)
        /// </summary>
        public static CurrencyCost GetCost(UpgradeKind k)
        {
            int n = GetLevel(k) + 1;
            switch (k)
            {
                case UpgradeKind.Damage:
                case UpgradeKind.Armor:
                    return new CurrencyCost((CurrencyType.Coin, n), (CurrencyType.Gold, n));
                case UpgradeKind.Skill1:
                    return new CurrencyCost((CurrencyType.Coin, n), (CurrencyType.Rune1, n));
                case UpgradeKind.Skill2:
                    return new CurrencyCost((CurrencyType.Coin, n), (CurrencyType.Rune2, n));
                case UpgradeKind.Skill3:
                    return new CurrencyCost((CurrencyType.Coin, n), (CurrencyType.Rune3, n));
                case UpgradeKind.ItemHeal:
                case UpgradeKind.ItemDash:
                    return new CurrencyCost((CurrencyType.Coin, n * 2), (CurrencyType.Gold, n));
                default:
                    return new CurrencyCost((CurrencyType.Coin, n));
            }
        }

        public static bool CanAfford(CurrencyCost cost)
        {
            foreach ((CurrencyType type, int amount) in cost.Entries())
            {
                if (GameManager.GetTotalCurrency(type) < amount)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool TryUpgrade(UpgradeKind k)
        {
            CurrencyCost cost = GetCost(k);
            if (!CanAfford(cost))
            {
                return false;
            }
            foreach ((CurrencyType type, int amount) in cost.Entries())
            {
                GameManager.AddTotalCurrency(type, -amount);
            }
            PlayerPrefs.SetInt(LevelKey(k), GetLevel(k) + 1);
            PlayerPrefs.Save();
            SfxPlayer.Play(SfxId.Upgrade);
            Changed?.Invoke();
            return true;
        }

        public static string DisplayName(UpgradeKind k)
        {
            switch (k)
            {
                case UpgradeKind.Damage: return "Damage";
                case UpgradeKind.Armor: return "Armor";
                case UpgradeKind.Skill1: return "Skill 1";
                case UpgradeKind.Skill2: return "Skill 2";
                case UpgradeKind.Skill3: return "Skill 3";
                case UpgradeKind.ItemHeal: return "Heal Item";
                case UpgradeKind.ItemDash: return "Dash Item";
                default: return k.ToString();
            }
        }

        /// <summary>강화 항목의 레벨당 효과 설명 (팝업 표시용).</summary>
        public static string EffectDescription(UpgradeKind k)
        {
            switch (k)
            {
                case UpgradeKind.Damage: return $"레벨당 대미지 +{DamagePerLevel:0}";
                case UpgradeKind.Armor: return $"레벨당 최대 HP +{HpPerLevel}";
                case UpgradeKind.Skill1:
                case UpgradeKind.Skill2:
                case UpgradeKind.Skill3: return "레벨당 해당 스킬 위력·지속 증가";
                case UpgradeKind.ItemHeal: return $"레벨당 회복량 +{HealItemPerLevel}";
                case UpgradeKind.ItemDash:
                    return $"{DashItemLevelsPerPass}레벨마다 왕복 +1 · 레벨당 관통 피해 +{DashItemDamagePerLevel:0.00}배";
                default: return string.Empty;
            }
        }

        /// <summary>스킬 슬롯(0~2)의 강화 레벨. 스킬 컴포넌트가 효과 계산에 사용.</summary>
        public static int SkillLevel(int slot)
        {
            switch (slot)
            {
                case 0: return GetLevel(UpgradeKind.Skill1);
                case 1: return GetLevel(UpgradeKind.Skill2);
                case 2: return GetLevel(UpgradeKind.Skill3);
                default: return 0;
            }
        }

        // ---- 아이템 강화 결과 (베이스는 아이템 프리팹이 넘겨줌) ----

        /// <summary>체력 회복 아이템 총 회복량 = 베이스 + 강화.</summary>
        public static int HealItemAmount(int baseAmount) =>
            Mathf.Max(0, baseAmount) + GetLevel(UpgradeKind.ItemHeal) * HealItemPerLevel;

        /// <summary>ㄹ자 대시 왕복 횟수 = 베이스 + 강화(DashItemLevelsPerPass 레벨마다 +1).</summary>
        public static int DashItemPasses(int basePasses) =>
            Mathf.Max(1, basePasses) + GetLevel(UpgradeKind.ItemDash) / Mathf.Max(1, DashItemLevelsPerPass);

        /// <summary>ㄹ자 대시 관통 피해 배수 = 베이스 + 강화.</summary>
        public static float DashItemDamageMul(float baseMul) =>
            baseMul + GetLevel(UpgradeKind.ItemDash) * DashItemDamagePerLevel;

        // ------------------------------------------------------------------
        // 계산된 스탯
        // ------------------------------------------------------------------

        public static int Damage => Mathf.RoundToInt(BaseDamage + GetLevel(UpgradeKind.Damage) * DamagePerLevel);
        public static int MaxHp => BaseMaxHp + GetLevel(UpgradeKind.Armor) * HpPerLevel;
        public static float CritChance => Mathf.Clamp(BaseCritChance + EquippedSwordCritBonus(), 0f, 100f);

        // ------------------------------------------------------------------
        // 장착 검
        // ------------------------------------------------------------------

        /// <summary>장착 중인 검. 명시 안 됐거나 미보유면 보유 중 최고 등급 자동.</summary>
        public static SwordRarity? EquippedSword
        {
            get
            {
                int raw = PlayerPrefs.GetInt(EquippedSwordKey, -1);
                if (raw >= 0 && Enum.IsDefined(typeof(SwordRarity), raw))
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
            SfxPlayer.Play(SfxId.UiClick);
            Changed?.Invoke();
        }

        private static bool IsOwned(SwordRarity r)
        {
            return Array.IndexOf(RunInventory.GetOwnedSwords(), r) >= 0;
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

        // ------------------------------------------------------------------
        // 치트 (에디터 메뉴 + 인게임 치트키 CheatKeys 가 공유)
        // ------------------------------------------------------------------

        public static bool GodMode => PlayerPrefs.GetInt(PlayerController.GodModeKey, 0) == 1;

        /// <summary>무적 토글. 재생 중이면 즉시 반영 + PlayerPrefs 저장.</summary>
        public static bool ToggleGodMode()
        {
            bool on = !GodMode;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.SetGodMode(on);
            }
            else
            {
                PlayerPrefs.SetInt(PlayerController.GodModeKey, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
            return on;
        }

        /// <summary>전 등급 검 보유 토글.</summary>
        public static bool ToggleAllSwordsUnlocked()
        {
            RunInventory.AllUnlocked = !RunInventory.AllUnlocked;
            Changed?.Invoke();
            return RunInventory.AllUnlocked;
        }

        /// <summary>모든 저장 데이터(강화·재화·최고기록·보유검) 초기화.</summary>
        public static void ResetAllProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("1655M/Reset Progress (PlayerPrefs)")]
        private static void ResetProgress() => ResetAllProgress();

        [UnityEditor.MenuItem("1655M/Debug: All Swords Unlocked")]
        private static void ToggleAllSwordsUnlockedMenu() => ToggleAllSwordsUnlocked();

        [UnityEditor.MenuItem("1655M/Debug: All Swords Unlocked", true)]
        private static bool ToggleAllSwordsUnlockedValidate()
        {
            UnityEditor.Menu.SetChecked("1655M/Debug: All Swords Unlocked", RunInventory.AllUnlocked);
            return true;
        }

        [UnityEditor.MenuItem("1655M/Debug: Player Invincible")]
        private static void TogglePlayerInvincible() => ToggleGodMode();

        [UnityEditor.MenuItem("1655M/Debug: Player Invincible", true)]
        private static bool TogglePlayerInvincibleValidate()
        {
            UnityEditor.Menu.SetChecked("1655M/Debug: Player Invincible",
                PlayerPrefs.GetInt(PlayerController.GodModeKey, 0) == 1);
            return true;
        }
#endif
    }
}
