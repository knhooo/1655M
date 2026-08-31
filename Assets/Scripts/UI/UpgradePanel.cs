using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Player;
using Game.Economy;
using Game.Flow;

namespace Game.UI
{
    /// <summary>
    /// 인벤토리 Status 탭의 단일 강화 패널.
    ///  - 선택 버튼들: 어떤 항목(Damage / Armor / Skill1~3)을 강화할지 고른다.
    ///  - 상세: 선택한 항목의 Lv / 비용 / [강화] 버튼. 텍스트만 바뀜.
    ///  - 하단: 현재 Damage / HP / Sword Damage 요약 (선택한 항목과 무관, 항상 최신).
    /// StatusPage 에 붙인다 (탭 전환으로 활성/비활성).
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        [Serializable]
        private class Selector
        {
            public Button button;
            public UpgradeKind kind;
        }

        [Tooltip("항목 선택 버튼들. button 과 kind 를 짝지어 연결.")]
        [SerializeField] private Selector[] _selectors;
        [Tooltip("처음 표시할 항목.")]
        [SerializeField] private UpgradeKind _selected = UpgradeKind.Damage;

        [Header("선택한 항목 상세 (선택)")]
        [SerializeField] private TMP_Text _nameText;    // "Damage"
        [SerializeField] private TMP_Text _levelText;   // "Lv.3"
        [SerializeField] private TMP_Text _costText;    // "Coin 4 / Gold 4"
        [SerializeField] private Button _upgradeButton;

        [Header("현재 스탯 요약 (Status 페이지 하단, 선택)")]
        [SerializeField] private TMP_Text _damageText;       // "Damage 23"
        [SerializeField] private TMP_Text _hpText;           // "HP 550"
        [SerializeField] private TMP_Text _swordDamageText;  // "Sword Damage 15%"

        private void Awake()
        {
            if (_selectors != null)
            {
                foreach (Selector s in _selectors)
                {
                    if (s != null && s.button != null)
                    {
                        UpgradeKind k = s.kind;
                        s.button.onClick.AddListener(() => Select(k));
                    }
                }
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(OnUpgrade);
            }
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

        /// <summary>선택 버튼에서 호출 (또는 코드).</summary>
        public void Select(UpgradeKind kind)
        {
            _selected = kind;
            Refresh();
        }

        /// <summary>인스펙터 onClick 에서 직접 쓰고 싶을 때용 (0=Damage, 1=Armor ...).</summary>
        public void Select(int kind) => Select((UpgradeKind)kind);

        private void OnUpgrade() => PlayerStats.TryUpgrade(_selected); // 성공 시 Changed → Refresh 자동

        private void Refresh()
        {
            int coin = GameManager.GetTotalCurrency(CurrencyType.Coin);
            int gold = GameManager.GetTotalCurrency(CurrencyType.Gold);
            CurrencyCost cost = PlayerStats.GetCost(_selected);

            if (_nameText != null)
            {
                _nameText.text = PlayerStats.DisplayName(_selected);
            }
            if (_levelText != null)
            {
                _levelText.text = $"Lv.{PlayerStats.GetLevel(_selected)}";
            }
            if (_costText != null)
            {
                _costText.text = $"Coin {cost.Coin} / Gold {cost.Gold}";
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = coin >= cost.Coin && gold >= cost.Gold;
            }

            // 하단 현재 스탯 요약
            if (_damageText != null)
            {
                _damageText.text = $"Damage {PlayerStats.Damage}";
            }
            if (_hpText != null)
            {
                _hpText.text = $"HP {PlayerStats.MaxHp}";
            }
            if (_swordDamageText != null)
            {
                _swordDamageText.text = $"Sword Damage {PlayerStats.CritChance:0}%";
            }

            // 선택된 항목 버튼은 눌린 것처럼 비활성 표시
            if (_selectors != null)
            {
                foreach (Selector s in _selectors)
                {
                    if (s != null && s.button != null)
                    {
                        s.button.interactable = s.kind != _selected;
                    }
                }
            }
        }
    }
}
