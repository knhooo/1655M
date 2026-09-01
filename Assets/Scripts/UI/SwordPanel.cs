using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Player;
using Game.Equipment;

namespace Game.UI
{
    /// <summary>
    /// 인벤토리 Sword 탭.
    ///  - 세로 스크롤 리스트에 등급 낮은 순(Normal→Ultimate)으로 무기 아이콘을 나열.
    ///  - 아이콘을 누르면 "선택"(하이라이트). 선택한 칸에만 그 무기 아래 치명타 확률 증가치 표시(슬롯이 처리).
    ///  - 중앙 하단 [SELECT] 버튼 → 선택한 무기 장착 (보유 & 미장착일 때만 활성).
    ///
    /// 배치: SwordPage 오브젝트에 붙인다. _content 는 ScrollRect 의 Content(세로 레이아웃).
    /// </summary>
    public class SwordPanel : MonoBehaviour
    {
        [Header("리스트")]
        [SerializeField] private SwordSlotView _slotPrefab;
        [Tooltip("ScrollRect 의 Content. VerticalLayoutGroup + ContentSizeFitter 권장.")]
        [SerializeField] private RectTransform _content;
        [Tooltip("등급별 아이콘 애셋. 비우면 프리팹 기본 아이콘 사용.")]
        [SerializeField] private SwordIconSet _iconSet;

        [Header("장착")]
        [Tooltip("선택 무기가 보유 & 미장착일 때만 활성. 장착 중이면 못 누르는 상태.")]
        [SerializeField] private Button _selectButton;

        [Range(0f, 1f)]
        [SerializeField] private float _lockedAlpha = 0.35f;

        private readonly List<SwordSlotView> _spawned = new List<SwordSlotView>();
        private SwordRarity? _selected;
        private bool _built;

        private void Awake()
        {
            BuildSlots();
            if (_selectButton != null)
            {
                _selectButton.onClick.AddListener(EquipSelected);
            }
        }

        private void BuildSlots()
        {
            if (_built || _slotPrefab == null || _content == null)
            {
                return;
            }
            _built = true;

            foreach (SwordRarity rarity in (SwordRarity[])Enum.GetValues(typeof(SwordRarity)))
            {
                SwordSlotView view = Instantiate(_slotPrefab, _content);
                view.Bind(rarity, SpriteFor(rarity));
                view.Clicked += Select;
                _spawned.Add(view);
            }
        }

        private Sprite SpriteFor(SwordRarity rarity)
        {
            return _iconSet != null ? _iconSet.Get(rarity) : null;
        }

        private void OnEnable()
        {
            _selected = null; // 탭 열 때마다 선택 초기화 → 치확 텍스트 비움
            PlayerStats.Changed += Refresh;
            if (RunInventory.Instance != null)
            {
                RunInventory.Instance.SwordFound += OnSwordFound;
            }
            Refresh();
        }

        private void OnDisable()
        {
            PlayerStats.Changed -= Refresh;
            if (RunInventory.Instance != null)
            {
                RunInventory.Instance.SwordFound -= OnSwordFound;
            }
        }

        private void OnSwordFound(SwordRarity _) => Refresh();

        private void Select(SwordRarity rarity)
        {
            _selected = rarity;
            Refresh();
        }

        private void EquipSelected()
        {
            if (_selected.HasValue)
            {
                PlayerStats.EquipSword(_selected.Value); // 미보유면 무시. 성공 시 Changed → Refresh
            }
        }

        private void Refresh()
        {
            HashSet<int> owned = OwnedSet();
            SwordRarity? equipped = PlayerStats.EquippedSword;

            foreach (SwordSlotView view in _spawned)
            {
                if (view == null)
                {
                    continue;
                }
                bool has = owned.Contains((int)view.Rarity);
                bool isSelected = _selected.HasValue && _selected.Value == view.Rarity;
                bool isEquipped = equipped.HasValue && equipped.Value == view.Rarity;
                view.SetState(has, isSelected, isEquipped, _lockedAlpha);
            }

            bool selectedOwned = _selected.HasValue && owned.Contains((int)_selected.Value);
            bool selectedEquipped = _selected.HasValue && equipped.HasValue && equipped.Value == _selected.Value;
            if (_selectButton != null)
            {
                _selectButton.interactable = selectedOwned && !selectedEquipped;
            }
        }

        private static HashSet<int> OwnedSet()
        {
            HashSet<int> owned = new HashSet<int>();
            foreach (SwordRarity r in RunInventory.GetOwnedSwords())
            {
                owned.Add((int)r);
            }
            if (RunInventory.Instance != null)
            {
                var found = RunInventory.Instance.Found; // 이번 런에서 주운 것도 보유로
                for (int i = 0; i < found.Count; i++) // 인터페이스 foreach 박싱 회피
                {
                    owned.Add((int)found[i]);
                }
            }
            return owned;
        }
    }
}
