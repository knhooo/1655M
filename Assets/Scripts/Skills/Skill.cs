using System;
using UnityEngine;
using Game.Player;

namespace Game.Skills
{
    /// <summary>
    /// 모든 스킬의 베이스. 쿨다운·발동 조건·활성 이벤트를 공통 처리한다.
    /// 구체 스킬은 <see cref="OnActivate"/> 만 구현하면 된다.
    /// 플레이어 GameObject 에 여러 개 붙이고 각자 <see cref="_slot"/> 을 지정하면
    /// <see cref="SkillSystem"/> 이 자동 수집한다.
    /// </summary>
    public abstract class Skill : MonoBehaviour
    {
        [Header("Skill")]
        [Tooltip("슬롯 번호(0~2). SkillSystem 슬롯 = 단축키 = 강화 항목(SkillLevel).")]
        [SerializeField, Range(0, 2)] private int _slot = 0;
        [SerializeField] private string _displayName = "Skill";
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private float _cooldown = 5f;

        /// <summary>SkillSystem 슬롯 · 단축키 인덱스 · 강화 항목(<see cref="PlayerStats.SkillLevel"/>).</summary>
        public int Slot => _slot;

        /// <summary>이 스킬의 강화 레벨.</summary>
        protected int UpgradeLevel => PlayerStats.SkillLevel(_slot);

        /// <summary>플레이어 (씬 싱글톤). 스킬을 어느 오브젝트에 붙여도 접근 가능.</summary>
        protected static PlayerController Player => PlayerController.Instance;

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
