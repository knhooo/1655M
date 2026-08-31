using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Player;

namespace Game.UI
{
    /// <summary>
    /// 인게임 HUD. 맨 위 = 현재 심도(m, 1행 = 1m), 그 아래 = HP 가로 바.
    /// 재화 표시는 <see cref="CurrencyDisplay"/> (Source = Run) 하나로 처리.
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

        private PlayerController _player;
        private int _maxHp = 1;

        private void OnEnable()
        {
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
            if (_player == null)
            {
                return;
            }
            _player.CellChanged -= OnCellChanged;
            _player.HpChanged -= OnHpChanged;
            _player.MaxHpChanged -= OnMaxHpChanged;
            _player = null;
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
