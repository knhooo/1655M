using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Player;

namespace Game.UI
{
    /// <summary>
    /// 인게임 HUD. 맨 위 = 현재 심도(m, 1행 = 1m), 그 아래 = HP 가로 바.
    /// 재화 표시는 <see cref="CurrencyDisplay"/> (Source = Run) 하나로 처리.
    /// HP 가 줄면 바가 잠깐 흰색으로 번쩍인다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("심도")]
        [SerializeField] private TMP_Text _depthText;
        [SerializeField] private string _depthSuffix = " m";

        [Header("HP 바")]
        [Tooltip("Image Type = Filled, Fill Method = Horizontal.")]
        [SerializeField] private Image _hpFill;
        [SerializeField] private TMP_Text _hpText;

        [Header("피격 플래시")]
        [Tooltip("HP 바 위에 겹치는 흰색 Image(Raycast Target 끔). 있으면 이걸 깜빡임 — 색과 무관하게 확실히 보임.")]
        [SerializeField] private Image _flashOverlay;
        [Tooltip("_flashOverlay 가 없을 때만: _hpFill 을 이 색으로 틴트.")]
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.15f;
        [Range(0f, 1f)][SerializeField] private float _flashOverlayMaxAlpha = 0.8f;

        private PlayerController _player;
        private int _maxHp = 1;

        private Color _hpFillColor = Color.white;
        private float _flashTimer;
        private int _lastHp = -1;

        private void Awake()
        {
            if (_hpFill != null)
            {
                _hpFillColor = _hpFill.color;
            }
            SetFlash(0f);
        }

        private void OnEnable()
        {
            _lastHp = -1;
            _flashTimer = 0f;
            SetFlash(0f);

            _player = PlayerController.Instance;
            if (_player == null)
            {
                return;
            }

            _player.CellChanged += OnCellChanged;
            _player.HpChanged += OnHpChanged;
            _player.MaxHpChanged += OnMaxHpChanged;

            _maxHp = Mathf.Max(1, _player.MaxHp);
            OnCellChanged(_player.Column, _player.Row);
            RefreshHp(_player.Hp);
        }

        private void OnDisable()
        {
            SetFlash(0f);
            if (_player == null)
            {
                return;
            }
            _player.CellChanged -= OnCellChanged;
            _player.HpChanged -= OnHpChanged;
            _player.MaxHpChanged -= OnMaxHpChanged;
            _player = null;
        }

        private void Update()
        {
            if (_flashTimer <= 0f)
            {
                return;
            }
            _flashTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_flashTimer / Mathf.Max(0.01f, _flashDuration));
            SetFlash(k);
        }

        /// <summary>k: 1 = 번쩍임 최대, 0 = 없음.</summary>
        private void SetFlash(float k)
        {
            if (_flashOverlay != null)
            {
                Color c = _flashOverlay.color;
                c.a = k * _flashOverlayMaxAlpha;
                _flashOverlay.color = c;
            }
            else if (_hpFill != null)
            {
                _hpFill.color = Color.Lerp(_hpFillColor, _flashColor, k);
            }
        }

        private void OnCellChanged(int col, int row)
        {
            if (_depthText != null)
            {
                _depthText.text = Mathf.Max(0, row) + _depthSuffix;
            }
        }

        private void OnMaxHpChanged(int max)
        {
            _maxHp = Mathf.Max(1, max);
            RefreshHp(_player != null ? _player.Hp : _maxHp);
        }

        private void OnHpChanged(int hp) => RefreshHp(hp);

        private void RefreshHp(int hp)
        {
            if (_lastHp >= 0 && hp < _lastHp)
            {
                _flashTimer = _flashDuration;
            }
            _lastHp = hp;

            float t = Mathf.Clamp01((float)hp / _maxHp);

            if (_hpFill != null)
            {
                _hpFill.fillAmount = t;
            }
            if (_hpText != null)
            {
                _hpText.text = $"{Mathf.Max(0, hp)} / {_maxHp}";
            }
        }
    }
}
