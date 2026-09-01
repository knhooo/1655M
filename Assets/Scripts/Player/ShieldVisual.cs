using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 보호막이 켜져 있는 <b>동안 계속</b> 링(자식 오브젝트)을 표시한다. 만료되면 숨김.
    /// 활성 중엔 천천히 회전 + 미세하게 맥동해서 살아있는 느낌.
    ///
    /// 배치: 플레이어(또는 자식)에 붙이고 _ring 에 링 스프라이트 오브젝트 연결.
    /// <see cref="PlayerController"/> 는 싱글톤으로 자동 참조.
    /// </summary>
    public class ShieldVisual : MonoBehaviour
    {
        [Tooltip("보호막 링 오브젝트 (스프라이트/파티클 등). 켜고 끄는 대상.")]
        [SerializeField] private GameObject _ring;
        [SerializeField] private float _spin = 60f;
        [SerializeField, Range(0f, 0.4f)] private float _pulseAmp = 0.06f;
        [SerializeField] private float _pulseHz = 2f;

        private PlayerController _pc;
        private bool _active;
        private float _t;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (_ring != null)
            {
                _baseScale = _ring.transform.localScale;
                _ring.SetActive(false);
            }
        }

        private void Start()
        {
            _pc = PlayerController.Instance;
            if (_pc != null)
            {
                _pc.ShieldChanged += OnShieldChanged;
                SetActive(_pc.ShieldActive);
            }
        }

        private void OnDestroy()
        {
            if (_pc != null)
            {
                _pc.ShieldChanged -= OnShieldChanged;
            }
        }

        private void OnShieldChanged(bool on) => SetActive(on);

        private void SetActive(bool on)
        {
            _active = on;
            if (_ring != null)
            {
                _ring.SetActive(on);
                if (on)
                {
                    _t = 0f;
                    _ring.transform.localScale = _baseScale;
                }
            }
        }

        private void Update()
        {
            if (!_active || _ring == null)
            {
                return;
            }
            _t += Time.deltaTime;
            _ring.transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            float pulse = 1f + Mathf.Sin(_t * _pulseHz * Mathf.PI * 2f) * _pulseAmp;
            _ring.transform.localScale = _baseScale * pulse;
        }
    }
}
