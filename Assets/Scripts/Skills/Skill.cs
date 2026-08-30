using System;
using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// 모든 스킬의 베이스. 쿨다운·발동 조건·활성 이벤트를 공통 처리한다.
    /// 구체 스킬은 <see cref="OnActivate"/> 만 구현하면 된다.
    /// 플레이어 GameObject 에 컴포넌트로 붙이고, <see cref="SkillSystem"/> 슬롯에 등록해 사용.
    /// </summary>
    public abstract class Skill : MonoBehaviour
    {
        [Header("Skill")]
        [SerializeField] private string _displayName = "Skill";
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private float _cooldown = 5f;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float Cooldown => _cooldown;
        public float CooldownRemaining { get; private set; }

        /// <summary>남은 쿨다운 비율(1 = 방금 사용, 0 = 준비 완료). 어두운 오버레이가 줄어드는 연출용.</summary>
        public float CooldownNormalized => _cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / _cooldown) : 0f;

        /// <summary>쿨다운 회복 진행도(0 = 방금 사용, 1 = 준비 완료). 아이콘이 0에서 차오르는 연출용.</summary>
        public float CooldownProgress => _cooldown > 0f ? Mathf.Clamp01(1f - CooldownRemaining / _cooldown) : 1f;

        public bool IsReady => CooldownRemaining <= 0f && CanActivate();

        /// <summary>발동에 성공한 순간.</summary>
        public event Action Activated;

        protected virtual void Update()
        {
            if (CooldownRemaining > 0f)
            {
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            }
        }

        /// <summary>키/버튼/슬롯에서 호출. 발동에 성공하면 true.</summary>
        public bool TryActivate()
        {
            if (CooldownRemaining > 0f || !CanActivate())
            {
                return false;
            }

            OnActivate();
            CooldownRemaining = _cooldown;
            Activated?.Invoke();
            return true;
        }

        /// <summary>지금 발동 가능한지(대상·상태 조건). 기본 true.</summary>
        protected virtual bool CanActivate() => true;

        /// <summary>실제 스킬 효과.</summary>
        protected abstract void OnActivate();
    }
}
