using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Player;
using Game.Economy;
using Game.Flow;

namespace Game.UI
{
    /// <summary>
    /// 인벤토리 Status 탭. Coin·Gold 를 소모해 대미지 / 체력을 강화한다.
    /// Lv 0 부터 시작(추가치 없음), 무한 레벨, 비용 선형(레벨당 +1씩).
    /// StatusPage 오브젝트에 붙인다 (탭 전환으로 활성/비활성).
    /// </summary>
    public class StatusPanelUI : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private TMP_Text _damageLevelText;
        [SerializeField] private TMP_Text _damageValueText;   // "17 → 19"
        [SerializeField] private TMP_Text _damageCostText;    // "Coin 1 / Gold 1"
        [SerializeField] private Button _damageButton;

        [Header("HP")]
        [SerializeField] private TMP_Text _hpLevelText;
        [SerializeField] private TMP_Text _hpValueText;
        [SerializeField] private TMP_Text _hpCostText;
        [SerializeField] private Button _hpButton;

        [Header("Crit (읽기 전용)")]
        [SerializeField] private TMP_Text _critValueText;

        private void Awake()
        {
            if (_damageButton != null) _damageButton.onClick.AddListener(OnUpgradeDamage);
            if (_hpButton != null) _hpButton.onClick.AddListener(OnUpgradeHp);
        }

        private void OnDestroy()
        {
            if (_damageButton != null) _damageButton.onClick.RemoveListener(OnUpgradeDamage);
            if (_hpButton != null) _hpButton.onClick.RemoveListener(OnUpgradeHp);
        }

        private void OnEnable()
        {
            PlayerStats.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerStats.Changed -= Refresh;
        }

        // 성공 시 PlayerStats.Changed → Refresh 가 자동 호출됨
        private void OnUpgradeDamage() => PlayerStats.TryUpgradeDamage();
        private void OnUpgradeHp() => PlayerStats.TryUpgradeHp();

        private void Refresh()
        {
            int coin = GameManager.GetTotalCurrency(CurrencyType.Coin);
            int gold = GameManager.GetTotalCurrency(CurrencyType.Gold);

            CurrencyCost dc = PlayerStats.DamageUpgradeCost;
            SetRow(_damageLevelText, _damageValueText, _damageCostText, _damageButton,
                PlayerStats.DamageLevel, PlayerStats.Damage, PlayerStats.NextDamage, dc, coin, gold);

            CurrencyCost hc = PlayerStats.HpUpgradeCost;
            SetRow(_hpLevelText, _hpValueText, _hpCostText, _hpButton,
                PlayerStats.HpLevel, PlayerStats.MaxHp, PlayerStats.NextMaxHp, hc, coin, gold);

            if (_critValueText != null)
            {
                _critValueText.text = $"{PlayerStats.CritChance:0}%";
            }
        }

        private static void SetRow(TMP_Text levelText, TMP_Text valueText, TMP_Text costText, Button button,
            int level, int current, int next, CurrencyCost cost, int coin, int gold)
        {
            if (levelText != null) levelText.text = $"Lv.{level}";
            if (valueText != null) valueText.text = $"{current} → {next}";
            if (costText != null) costText.text = $"Coin {cost.Coin} / Gold {cost.Gold}";
            if (button != null) button.interactable = coin >= cost.Coin && gold >= cost.Gold;
        }
    }
}
