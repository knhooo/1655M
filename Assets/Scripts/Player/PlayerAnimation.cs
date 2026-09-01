using System.Collections;
using UnityEngine;
using Game.Skills;

namespace Game.Player
{
    /// <summary>
    /// 플레이어 <b>몸</b>의 애니메이션·비주얼을 한곳에서 관리한다. (장착 무기는 <c>PlayerWeapon</c> 이 따로)
    ///  - 피격 플래시 : <see cref="PlayerController.DamageTaken"/>
    ///  - 사망 스캐터 : <see cref="PlayerController.Died"/> — 흰 플래시 → 튕겨오르며 회전·축소·페이드 + 파티클
    ///  - 보호막 링   : <see cref="PlayerController.ShieldChanged"/> — 켜진 동안 회전 + 맥동
    ///  - 스킬 발동 이펙트 : 각 <see cref="Skill.Activated"/> — 그 스킬의 <see cref="Skill.CastEffect"/> 를 팝 스케일 + 페이드
    ///
    /// 히트스톱·카메라 흔들림은 <c>JuiceDirector</c> 가 동일 이벤트로 처리한다.
    /// 배치: 플레이어 루트에 하나. 참조는 인스펙터에서 연결(몸 스프라이트는 비우면 자식에서 자동 탐색).
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("몸 스프라이트")]
        [Tooltip("플레이어 몸 스프라이트. 비우면 자식에서 자동 탐색 (무기/이펙트/링 이름은 제외).")]
        [SerializeField] private SpriteRenderer _body;

        [Header("피격 플래시")]
        [SerializeField] private bool _hurtFlashEnabled = true;
        [SerializeField] private Color _hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private float _hurtFlashTime = 0.12f;

        [Header("사망 — 파티클 (선택)")]
        [Tooltip("사망 위치에 Instantiate. ParticleSystem 이든 아니든 됨.")]
        [SerializeField] private GameObject _deathBurstPrefab;
        [Tooltip("미리 배치해 둔 ParticleSystem. 사망 시 Play().")]
        [SerializeField] private ParticleSystem _deathBurst;
        [SerializeField] private float _deathBurstLifetime = 2f;
        [Tooltip("사망 시 숨길 자식 (장착 무기 등).")]
        [SerializeField] private GameObject[] _hideOnDeath;

        [Header("사망 — 몸 스캐터")]
        [Tooltip("사망 연출이 끝날 때까지 GameManager 가 카메라 되감기를 미루는 시간(초). "
               + "파티클이 다 보이도록 스캐터·파티클 길이보다 넉넉히.")]
        [SerializeField] private float _deathHold = 1f;
        [SerializeField] private float _deathDuration = 0.55f;
        [SerializeField] private float _deathFlashTime = 0.06f;
        [Tooltip("튕겨오르는 높이(월드 단위).")]
        [SerializeField] private float _deathPopUp = 0.6f;
        [Tooltip("회전 속도(도/초).")]
        [SerializeField] private float _deathSpin = 540f;
        [Tooltip("끝날 때 크기 배수.")]
        [SerializeField] private float _deathEndScale = 0.1f;

        [Header("보호막 링")]
        [Tooltip("보호막 링 오브젝트. 켜진 동안 표시 + 회전/맥동.")]
        [SerializeField] private GameObject _shieldRing;
        [SerializeField] private float _shieldSpin = 60f;
        [SerializeField, Range(0f, 0.4f)] private float _shieldPulseAmp = 0.06f;
        [SerializeField] private float _shieldPulseHz = 2f;

        [Header("스킬 발동 이펙트")]
        [SerializeField] private SkillSystem _skillSystem;
        [Tooltip("스킬 발동 이펙트 전용 SpriteRenderer (기본 비활성).")]
        [SerializeField] private SpriteRenderer _castRenderer;
        [SerializeField] private float _castDuration = 0.25f;
        [SerializeField] private float _castStartScale = 0.5f;
        [SerializeField] private float _castEndScale = 1.4f;
        [SerializeField] private bool _castFade = true;

        private PlayerController _pc;
        private Color _bodyBaseColor = Color.white;
        private bool _dead;

        /// <summary>사망 연출이 진행 중. <c>GameManager</c> 가 카메라 되감기 전에 이게 꺼질 때까지 대기한다.</summary>
        public bool DeathSequenceActive { get; private set; }

        private bool _shieldOn;
        private float _shieldT;
        private Vector3 _shieldBaseScale = Vector3.one;

        private Color _castBaseColor = Color.white;
        private Coroutine _castCo;
        private Coroutine _hurtCo;

        private void Awake()
        {
            if (_body == null)
            {
                _body = FindBody();
            }
            if (_body != null)
            {
                _bodyBaseColor = _body.color;
            }

            if (_shieldRing != null)
            {
                _shieldBaseScale = _shieldRing.transform.localScale;
                _shieldRing.SetActive(false);
            }

            if (_castRenderer != null)
            {
                _castBaseColor = _castRenderer.color;
                _castRenderer.enabled = false;
            }
        }

        private void Start()
        {
            _pc = PlayerController.Instance;
            if (_pc != null)
            {
                _pc.DamageTaken += OnDamageTaken;
                _pc.Died += OnDied;
                _pc.ShieldChanged += OnShieldChanged;
                SetShield(_pc.ShieldActive);
            }

            if (_skillSystem == null)
            {
                _skillSystem = GetComponentInParent<SkillSystem>();
            }
            if (_skillSystem == null)
            {
                _skillSystem = FindFirstObjectByType<SkillSystem>();
            }
            if (_skillSystem != null)
            {
                for (int i = 0; i < _skillSystem.SlotCount; i++)
                {
                    Skill s = _skillSystem.GetSlot(i);
                    if (s != null)
                    {
                        Skill captured = s;
                        s.Activated += () => OnSkillCast(captured);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_pc != null)
            {
                _pc.DamageTaken -= OnDamageTaken;
                _pc.Died -= OnDied;
                _pc.ShieldChanged -= OnShieldChanged;
            }
            // 스킬 Activated 는 익명 구독 — 스킬과 함께 씬 리로드 시 정리됨
        }

        private void Update()
        {
            if (_shieldOn && _shieldRing != null)
            {
                _shieldT += Time.deltaTime;
                _shieldRing.transform.Rotate(0f, 0f, _shieldSpin * Time.deltaTime);
                float pulse = 1f + Mathf.Sin(_shieldT * _shieldPulseHz * Mathf.PI * 2f) * _shieldPulseAmp;
                _shieldRing.transform.localScale = _shieldBaseScale * pulse;
            }
        }

        private SpriteRenderer FindBody()
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                string n = sr.gameObject.name.ToLowerInvariant();
                if (n.Contains("weapon") || n.Contains("effect") || n.Contains("slash") ||
                    n.Contains("shield") || n.Contains("ring") || n.Contains("cast"))
                {
                    continue; // 연출용 스프라이트는 제외
                }
                return sr;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 피격
        // ------------------------------------------------------------------

        private void OnDamageTaken(int amount, Vector3 source)
        {
            if (_dead || !_hurtFlashEnabled || _body == null)
            {
                return;
            }
            if (_hurtCo != null)
            {
                StopCoroutine(_hurtCo);
            }
            _hurtCo = StartCoroutine(HurtFlash());
        }

        private IEnumerator HurtFlash()
        {
            float dur = Mathf.Max(0.02f, _hurtFlashTime);
            float t = 0f;
            while (t < dur && !_dead)
            {
                t += Time.deltaTime;
                float k = t / dur;
                // 0 → 확 물들었다가 → 원래대로
                float w = 1f - Mathf.Abs(k * 2f - 1f);
                _body.color = Color.Lerp(_bodyBaseColor, _hurtFlashColor, w);
                yield return null;
            }
            if (!_dead)
            {
                _body.color = _bodyBaseColor;
            }
            _hurtCo = null;
        }

        // ------------------------------------------------------------------
        // 사망
        // ------------------------------------------------------------------

        private void OnDied()
        {
            if (_dead)
            {
                return;
            }
            _dead = true;
            DeathSequenceActive = true;
            if (_hurtCo != null)
            {
                StopCoroutine(_hurtCo);
                _hurtCo = null;
            }

            Vector3 pos = _body != null ? _body.transform.position : transform.position;

            if (_deathBurstPrefab != null)
            {
                GameObject g = Instantiate(_deathBurstPrefab, pos, Quaternion.identity);
                g.SetActive(true);
                // 프리팹의 PS 가 Play On Awake 가 아니어도 확실히 재생
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Play(true);
                }
                Destroy(g, Mathf.Max(0.1f, _deathBurstLifetime));
            }
            if (_deathBurst != null)
            {
                // 몸 스캐터(회전·축소)에 딸려가지 않도록 월드로 분리 후 재생
                _deathBurst.transform.SetParent(null, true);
                if (!_deathBurst.gameObject.activeSelf)
                {
                    _deathBurst.gameObject.SetActive(true); // 꺼둔 상태면 켜야 재생됨
                }
                _deathBurst.Clear(true);
                _deathBurst.Play(true);
            }

            if (_deathBurstPrefab == null && _deathBurst == null)
            {
                Debug.LogWarning("[PlayerAnimation] 사망 파티클 미할당 — _deathBurstPrefab 또는 _deathBurst 를 인스펙터에서 연결하세요.", this);
            }

            if (_shieldRing != null)
            {
                _shieldRing.SetActive(false);
            }
            if (_hideOnDeath != null)
            {
                for (int i = 0; i < _hideOnDeath.Length; i++)
                {
                    if (_hideOnDeath[i] != null)
                    {
                        _hideOnDeath[i].SetActive(false);
                    }
                }
            }

            if (_body != null)
            {
                StartCoroutine(DeathScatter());
            }
            StartCoroutine(DeathHold());
        }

        private IEnumerator DeathHold()
        {
            float t = 0f;
            float hold = Mathf.Max(0f, _deathHold);
            while (t < hold)
            {
                t += Time.unscaledDeltaTime; // 히트스톱 중에도 흐르게
                yield return null;
            }
            DeathSequenceActive = false;
        }

        private IEnumerator DeathScatter()
        {
            Transform t = _body.transform;
            Vector3 startPos = t.position;
            Vector3 startScale = t.localScale;
            Quaternion startRot = t.rotation;
            int dir = Random.value < 0.5f ? -1 : 1;

            float f = 0f;
            float flash = Mathf.Max(0.01f, _deathFlashTime);
            while (f < flash)
            {
                f += Time.deltaTime;
                _body.color = Color.Lerp(_bodyBaseColor, Color.white, Mathf.Clamp01(f / flash));
                yield return null;
            }

            float dur = Mathf.Max(0.05f, _deathDuration);
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);

                float arc = Mathf.Sin(k * Mathf.PI) * _deathPopUp;
                t.position = startPos + new Vector3(dir * 0.25f * k, arc, 0f);
                t.rotation = startRot * Quaternion.Euler(0f, 0f, dir * _deathSpin * e);
                t.localScale = Vector3.Lerp(startScale, startScale * _deathEndScale, k * k);

                Color c = Color.Lerp(Color.white, _bodyBaseColor, 0.3f);
                c.a = 1f - k;
                _body.color = c;
                yield return null;
            }

            _body.enabled = false;
        }

        // ------------------------------------------------------------------
        // 보호막 링
        // ------------------------------------------------------------------

        private void OnShieldChanged(bool on) => SetShield(on);

        private void SetShield(bool on)
        {
            _shieldOn = on && !_dead;
            if (_shieldRing != null)
            {
                _shieldRing.SetActive(_shieldOn);
                if (_shieldOn)
                {
                    _shieldT = 0f;
                    _shieldRing.transform.localScale = _shieldBaseScale;
                }
            }
        }

        // ------------------------------------------------------------------
        // 스킬 발동 이펙트
        // ------------------------------------------------------------------

        private void OnSkillCast(Skill skill)
        {
            if (_dead || _castRenderer == null || skill == null || skill.CastEffect == null)
            {
                return;
            }
            _castRenderer.sprite = skill.CastEffect;
            if (_castCo != null)
            {
                StopCoroutine(_castCo);
            }
            _castCo = StartCoroutine(CastPlay());
        }

        private IEnumerator CastPlay()
        {
            _castRenderer.enabled = true;
            float t = 0f;
            float dur = Mathf.Max(0.01f, _castDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);

                float s = Mathf.Lerp(_castStartScale, _castEndScale, k);
                _castRenderer.transform.localScale = new Vector3(s, s, 1f);

                if (_castFade)
                {
                    Color c = _castBaseColor;
                    c.a = _castBaseColor.a * (1f - k);
                    _castRenderer.color = c;
                }
                yield return null;
            }
            _castRenderer.color = _castBaseColor;
            _castRenderer.enabled = false;
            _castCo = null;
        }
    }
}
