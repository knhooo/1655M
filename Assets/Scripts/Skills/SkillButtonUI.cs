using UnityEngine;
using UnityEngine.UI;

namespace Game.Skills
{
    /// <summary>
    /// UGUI 버튼 1개를 스킬 슬롯에 연결한다.
    ///  - 클릭 → 해당 슬롯 스킬 발동
    ///  - 쿨다운을 Filled 이미지로 표시, 준비되면 버튼 활성화
    /// 버튼 오브젝트에 붙이고 참조를 인스펙터에서 연결.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SkillButtonUI : MonoBehaviour
    {
        [SerializeField] private SkillSystem _system;
        [SerializeField, Min(0)] private int _slot;
        [SerializeField] private Button _button;

        [Tooltip("Image Type = Filled 로 설정. 쿨다운 진행(1→0)을 표시.")]
        [SerializeField] private Image _cooldownFill;

        [SerializeField] private Image _iconImage;

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
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void Start()
        {
            Skill skill = _system != null ? _system.GetSlot(_slot) : null;
            if (skill == null)
            {
                return;
            }

            if (_iconImage != null && skill.Icon != null)
            {
                _iconImage.sprite = skill.Icon;
            }

            // 시작 시 꽉 찬 상태로.
            if (_cooldownFill != null)
            {
                _cooldownFill.fillAmount = skill.CooldownProgress;
            }
        }

        private void OnClick()
        {
            if (_system != null)
            {
                _system.UseSlot(_slot);
            }
        }

        private void Update()
        {
            Skill skill = _system != null ? _system.GetSlot(_slot) : null;
            if (skill == null)
            {
                return;
            }

            if (_cooldownFill != null)
            {
                // 준비 완료 = 꽉 참(1), 사용 직후 = 0 에서 점점 차오름.
                _cooldownFill.fillAmount = skill.CooldownProgress;
            }

            if (_button != null)
            {
                _button.interactable = skill.IsReady;
            }
        }
    }
}
