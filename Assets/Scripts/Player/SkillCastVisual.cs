using System.Collections;
using UnityEngine;
using Game.Skills;

namespace Game.Player
{
    /// <summary>
    /// 스킬 3종(돌진·보호막·충격파) 공통 발동 이펙트. 발동 시 그 스킬의
    /// <see cref="Skill.CastEffect"/> 스프라이트를 플레이어 위치에 잠깐 띄운다 (팝 스케일 + 페이드).
    /// 예전의 ShieldVisual / ShockwaveVisual 을 대체.
    ///
    /// 배치: 플레이어(또는 자식)에 붙이고 _renderer 에 전용 SpriteRenderer(기본 비활성) 연결.
    /// SkillSystem 은 자동으로 찾는다. 이펙트 스프라이트는 각 Skill 컴포넌트에서 지정.
    /// (주의: 보호막은 지속 버프지만 이펙트는 발동 순간 1회만 표시된다.)
    /// </summary>
    public class SkillCastVisual : MonoBehaviour
    {
        [SerializeField] private SkillSystem _system;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private float _startScale = 0.5f;
        [SerializeField] private float _endScale = 1.4f;
        [SerializeField] private bool _fade = true;

        private Color _baseColor = Color.white;
        private Coroutine _co;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
                _renderer.enabled = false;
            }
        }

        private void Start()
        {
            if (_system == null)
            {
                _system = GetComponentInParent<SkillSystem>();
            }
            if (_system == null)
            {
                _system = FindFirstObjectByType<SkillSystem>();
            }
            if (_system == null)
            {
                return;
            }

            for (int i = 0; i < _system.SlotCount; i++)
            {
                Skill s = _system.GetSlot(i);
                if (s != null)
                {
                    Skill captured = s;
                    s.Activated += () => OnCast(captured);
                }
            }
        }

        private void OnCast(Skill skill)
        {
            if (_renderer == null || skill == null || skill.CastEffect == null)
            {
                return;
            }

            _renderer.sprite = skill.CastEffect;
            if (_co != null)
            {
                StopCoroutine(_co);
            }
            _co = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            _renderer.enabled = true;
            float t = 0f;
            float dur = Mathf.Max(0.01f, _duration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);

                float s = Mathf.Lerp(_startScale, _endScale, k);
                _renderer.transform.localScale = new Vector3(s, s, 1f);

                if (_fade)
                {
                    Color c = _baseColor;
                    c.a = _baseColor.a * (1f - k);
                    _renderer.color = c;
                }
                yield return null;
            }
            _renderer.color = _baseColor;
            _renderer.enabled = false;
            _co = null;
        }
    }
}
