using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Equipment;

namespace Game.UI
{
    /// <summary>
    /// Sword 탭 스크롤 리스트의 한 칸. 무기 아이콘 하나 + 선택/장착 표시.
    /// 프리팹으로 만들어 <see cref="SwordPanel"/> 가 등급 수만큼 복제한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SwordSlotView : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트의 Button 사용.")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [Tooltip("선택 상태 하이라이트(테두리 등). 선택.")]
        [SerializeField] private GameObject _selectedFrame;
        [Tooltip("현재 장착 중 표시. 선택.")]
        [SerializeField] private GameObject _equippedMark;
        [Tooltip("등급 이름 라벨. 선택.")]
        [SerializeField] private TMP_Text _nameText;
        [Tooltip("무기 아래 치명타 확률 증가치. 이 칸을 선택했을 때만 표시.")]
        [SerializeField] private TMP_Text _critText;
        [SerializeField] private string _critFormat = "+{0}% Crit";

        public SwordRarity Rarity { get; private set; }

        /// <summary>이 칸을 눌렀을 때.</summary>
        public event Action<SwordRarity> Clicked;

        private void Reset()
        {
            _button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
            _button.onClick.AddListener(() => Clicked?.Invoke(Rarity));
        }

        public void Bind(SwordRarity rarity, Sprite iconSprite)
        {
            Rarity = rarity;
            if (_icon != null && iconSprite != null)
            {
                _icon.sprite = iconSprite;
            }
            if (_nameText != null)
            {
                _nameText.text = rarity.DisplayName();
            }
        }

        public void SetState(bool owned, bool selected, bool equipped, float lockedAlpha)
        {
            if (_icon != null)
            {
                Color c = _icon.color;
                c.a = owned ? 1f : lockedAlpha;
                _icon.color = c;
            }
            if (_selectedFrame != null)
            {
                _selectedFrame.SetActive(selected);
            }
            if (_equippedMark != null)
            {
                _equippedMark.SetActive(equipped);
            }
            if (_critText != null)
            {
                _critText.text = selected
                    ? string.Format(_critFormat, Mathf.RoundToInt(Rarity.CritChanceBonus()))
                    : string.Empty;
            }
        }
    }
}
