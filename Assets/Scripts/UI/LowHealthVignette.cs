using UnityEngine;
using UnityEngine.UI;
using Game.Player;

namespace Game.UI
{
    /// <summary>
    /// 플레이어 HP 가 낮으면 화면 전체를 빨갛게 깜빡인다. HP 가 낮을수록 빠르고 진하게.
    ///
    /// 배치: 화면을 꽉 채우는 빨강 <see cref="Image"/>(Raycast Target 끔) 오브젝트에 붙인다.
    /// HUD 캔버스 맨 위(다른 UI 위)에 두는 걸 권장. 알파는 스크립트가 제어하므로 0 이 아니어도 됨.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class LowHealthVignette : MonoBehaviour
    {
        [SerializeField] private Image _image;

        [Tooltip("이 HP 비율 이하에서 깜빡임 시작.")]
        [Range(0f, 1f)][SerializeField] private float _threshold = 0.3f;
        [Tooltip("가장 위험할 때 최대 알파.")]
        [Range(0f, 1f)][SerializeField] private float _maxAlpha = 0.45f;
        [Tooltip("깜빡임 속도 (임계치 근처 → 빈사).")]
        [SerializeField] private float _minPulseSpeed = 2.5f;
        [SerializeField] private float _maxPulseSpeed = 7f;

        private PlayerController _player;
        private float _fraction = 1f;
        private float _phase;

        private void Reset() => _image = GetComponent<Image>();

        private void Awake()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            _player = PlayerController.Instance;
            if (_player != null)
            {
                _player.HpChanged += OnHpChanged;
                _player.MaxHpChanged += OnMaxHpChanged;
                OnHpChanged(_player.Hp);
            }
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.HpChanged -= OnHpChanged;
                _player.MaxHpChanged -= OnMaxHpChanged;
                _player = null;
            }
            SetAlpha(0f);
        }

        private void OnMaxHpChanged(int max)
        {
            if (_player != null)
            {
                OnHpChanged(_player.Hp);
            }
        }

        private void OnHpChanged(int hp)
        {
            int max = _player != null ? Mathf.Max(1, _player.MaxHp) : 1;
            _fraction = Mathf.Clamp01((float)hp / max);
        }

        private void Update()
        {
            // 안전 구간이거나 사망 → 서서히 사라짐
            if (_fraction >= _threshold || _fraction <= 0f)
            {
                if (_image != null && _image.color.a > 0f)
                {
                    SetAlpha(Mathf.MoveTowards(_image.color.a, 0f, Time.unscaledDeltaTime * 2f));
                }
                return;
            }

            float danger = 1f - Mathf.Clamp01(_fraction / _threshold); // 0(임계치) → 1(빈사)
            float speed = Mathf.Lerp(_minPulseSpeed, _maxPulseSpeed, danger);
            _phase += Time.unscaledDeltaTime * speed;

            float pulse = Mathf.Sin(_phase) * 0.5f + 0.5f;
            float ceiling = Mathf.Lerp(_maxAlpha * 0.4f, _maxAlpha, danger);
            SetAlpha(pulse * ceiling);
        }

        private void SetAlpha(float a)
        {
            if (_image == null)
            {
                return;
            }
            Color c = _image.color;
            c.a = a;
            _image.color = c;
        }
    }
}
