using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Skills
{
    /// <summary>
    /// 최대 3개 스킬 슬롯 관리.
    ///  - PC: 슬롯별 단축키 (기본 Z / X / C)
    ///  - UI: 버튼 onClick 에서 <see cref="UseSlot"/> 호출 (또는 <see cref="SkillButtonUI"/> 사용)
    /// 플레이어 GameObject 에 붙이고, 같은 오브젝트의 Skill 컴포넌트들을 슬롯에 드래그.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        [Tooltip("슬롯 0/1/2. 같은 오브젝트에 붙은 Skill 컴포넌트를 등록.")]
        [SerializeField] private Skill[] _slots = new Skill[3];

        [Tooltip("각 슬롯의 PC 단축키.")]
        [SerializeField] private Key[] _slotKeys = { Key.Z, Key.X, Key.C };

        [Tooltip("체크 시 단축키 입력을 받는다.")]
        [SerializeField] private bool _keyboardEnabled = true;

        public int SlotCount => _slots != null ? _slots.Length : 0;

        public Skill GetSlot(int index)
        {
            return (_slots != null && index >= 0 && index < _slots.Length) ? _slots[index] : null;
        }

        private void Update()
        {
            if (!_keyboardEnabled || _slots == null)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            int count = Mathf.Min(_slots.Length, _slotKeys != null ? _slotKeys.Length : 0);
            for (int i = 0; i < count; i++)
            {
                if (kb[_slotKeys[i]].wasPressedThisFrame)
                {
                    UseSlot(i);
                }
            }
        }

        /// <summary>UI 버튼 onClick 에 연결하거나 코드에서 직접 호출.</summary>
        public void UseSlot(int index)
        {
            Skill skill = GetSlot(index);
            if (skill != null)
            {
                skill.TryActivate();
            }
        }
    }
}
