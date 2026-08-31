using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Skills
{
    /// <summary>
    /// 최대 3개 스킬 슬롯 관리.
    ///  - 같은 오브젝트의 <see cref="Skill"/> 컴포넌트들을 <see cref="Skill.Slot"/> 번호대로 자동 수집 (드래그 X)
    ///  - PC: 슬롯별 단축키 (기본 Z / X / C)
    ///  - UI: 버튼에서 <see cref="UseSlot"/> 호출 (또는 <see cref="SkillButtonUI"/>)
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        private const int SlotMax = 3;

        [Tooltip("각 슬롯의 PC 단축키.")]
        [SerializeField] private Key[] _slotKeys = { Key.Z, Key.X, Key.C };

        [Tooltip("체크 시 단축키 입력을 받는다.")]
        [SerializeField] private bool _keyboardEnabled = true;

        private readonly Skill[] _slots = new Skill[SlotMax];

        public int SlotCount => SlotMax;

        private void Awake()
        {
            foreach (Skill s in GetComponents<Skill>())
            {
                int i = s.Slot;
                if (i < 0 || i >= SlotMax)
                {
                    Debug.LogWarning($"[SkillSystem] {s.GetType().Name} 의 Slot 값 {i} 이 범위 밖 (0~{SlotMax - 1})", this);
                    continue;
                }
                if (_slots[i] != null)
                {
                    Debug.LogWarning($"[SkillSystem] 슬롯 {i} 중복: {_slots[i].GetType().Name} vs {s.GetType().Name}", this);
                }
                _slots[i] = s;
            }
        }

        public Skill GetSlot(int index)
        {
            return (index >= 0 && index < SlotMax) ? _slots[index] : null;
        }

        private void Update()
        {
            if (!_keyboardEnabled)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null || _slotKeys == null)
            {
                return;
            }

            int count = Mathf.Min(SlotMax, _slotKeys.Length);
            for (int i = 0; i < count; i++)
            {
                if (kb[_slotKeys[i]].wasPressedThisFrame)
                {
                    UseSlot(i);
                }
            }
        }

        /// <summary>UI 버튼에서 연결하거나 코드에서 직접 호출.</summary>
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
