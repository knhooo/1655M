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
    /// 인벤토리 Status 탭.
    ///  - 하단 요약: 현재 Damage / HP / Sword Damage (항상 표시).
    ///  - 선택 버튼(Damage/Armor/Skill1~3) 클릭 → 강화 팝업 활성화.
    ///  - 팝업: 그 항목의 Lv / 비용 / [강화]. 바깥(배경) 클릭 시 닫힘.
    ///  - 팝업이 떠 있는 동안 배경이 클릭을 막아 다른 버튼 못 누름 (모달).
    ///
    /// 이 컴포넌트는 StatusPage(항상 활성) 에 붙인다. 팝업 자체는 _popupRoot 로 토글.
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        [Serializable]
        private class Selector
        {
            public Button button;
            public UpgradeKind kind;
        }

        [Header("항목 선택 버튼 (StatusPage 상시 표시)")]
        [SerializeField] private Selector[] _selectors;

        [Header("팝업")]
        [Tooltip("강화 팝업 루트. 기본 비활성. 선택 버튼 누르면 켜짐.")]
        [SerializeField] private GameObject _popupRoot;
        [Tooltip("팝업 뒤 전체를 덮는 배경 버튼. 클릭 시 팝업 닫힘 + 뒤 버튼 클릭 차단.")]
        [SerializeField] private Button _backdropButton;
        [SerializeField] private TMP_Text _nameText;    // "Damage"
        [SerializeField] private TMP_Text _levelText;   // "Lv.3"
        [SerializeField] private TMP_Text _costText;    // "Coin 4 / Gold 4"
        [SerializeField] private Button _upgradeButton;

        [Header("현재 스탯 요약 (하단, 상시 표시)")]
        [SerializeField] private TMP_Text _damageText;       // "Damage 23"
        [SerializeField] private TMP_Text _hpText;           // "HP 550"
        [SerializeField] private TMP_Text _swordDamageText;  // "Sword Damage 15%"

        private UpgradeKind _selected;

        private void Awake()
        {
            if (_selectors != null)
            {
                foreach (Selector s in _selectors)
                {
                    if (s != null && s.button != null)
                    {
                        UpgradeKind k = s.kind;
                        s.button.onClick.AddListener(() => OpenFor(k));
                    }
                }
            }
            if (_backdropButton != null)
            {
                _backdropButton.onClick.AddListener(Close);
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(OnUpgrade);
            }

            if (_popupRoot != null)
            {
                _popupRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            PlayerStats.Changed += Refresh;
            if (_popupRoot != null)
            {
                _popupRoot.SetActive(false); // 탭 다시 열 때는 팝업 닫힌 상태
            }
            Refresh();
        }

        private void OnDisable()
        {
            PlayerStats.Changed -= Refresh;
        }

        /// <summary>선택 버튼에서 호출. 팝업을 그 항목으로 연다.</summary>
        public void OpenFor(UpgradeKind kind)
        {
            _selected = kind;
            if (_popupRoot != null)
            {
                _popupRoot.SetActive(true);
            }
            Refresh();
        }

        /// <summary>인스펙터 onClick 용 (0=Damage, 1=Armor ...).</summary>
        public void OpenFor(int kind) => OpenFor((UpgradeKind)kind);

        public void Close()
        {
            if (_popupRoot != null)
            {
                _popupRoot.SetActive(false);
            }
        }

        private void OnUpgrade() => PlayerStats.TryUpgrade(_selected); // 성공 시 Changed → Refresh 자동

        private void Refresh()
        {
            // 하단 요약 (항상)
            if (_damageText != null) _damageText.text = $"Damage {PlayerStats.Damage}";
            if (_hpText != null) _hpText.text = $"HP {PlayerStats.MaxHp}";
            if (_swordDamageText != null) _swordDamageText.text = $"Sword Damage {PlayerStats.CritChance:0}%";

            // 팝업 상세
            CurrencyCost cost = PlayerStats.GetCost(_selected);
            if (_nameText != null) _nameText.text = PlayerStats.DisplayName(_selected);
            if (_levelText != null) _levelText.text = $"Lv.{PlayerStats.GetLevel(_selected)}";
            if (_costText != null) _costText.text = BuildCostText(cost);
            if (_upgradeButton != null) _upgradeButton.interactable = PlayerStats.CanAfford(cost);
        }

        private static string BuildCostText(CurrencyCost cost)
        {
            _sb.Clear();
            bool first = true;
            foreach ((CurrencyType type, int amount) in cost.Entries())
            {
                if (!first) _sb.Append("  /  ");
                _sb.Append(type.DisplayName()).Append(' ').Append(amount);
                first = false;
            }
            return _sb.ToString();
        }

        private static readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(64);
    }
}
