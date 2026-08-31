using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 HP 바. 처음엔(풀 피) 숨겨져 있고, 한 대라도 맞으면 나타난다. 맞을 때 흰색으로 번쩍.
    ///
    /// 배치: <see cref="Enemy"/> 와 **같은 오브젝트**에 붙인다 (풀링 활성/비활성을 그대로 따라가도록).
    ///  - _barRoot : 배경+채움 스프라이트를 담은 자식 오브젝트. 이 스크립트가 켜고 끈다.
    ///  - _fill    : HP 비율만큼 localScale.x 가 줄어드는 채움 바(또는 왼쪽 정렬된 앵커).
    ///  - _fillRenderer: 번쩍임용. 비우면 _fill 하위에서 자동으로 찾는다.
    /// </summary>
    public class EnemyHpBar : MonoBehaviour
    {
        [SerializeField] private Enemy _enemy;
        [Tooltip("배경+채움을 담은 자식 오브젝트. 표시/숨김 대상.")]
        [SerializeField] private GameObject _barRoot;
        [Tooltip("HP 비율만큼 localScale.x 가 줄어드는 채움 바.")]
        [SerializeField] private Transform _fill;
        [Tooltip("HP 가 가득 찼을 때도 계속 보이게 하려면 해제.")]
        [SerializeField] private bool _hideWhenFull = true;

        [Header("피격 플래시")]
        [Tooltip("비우면 _fill 하위 SpriteRenderer 를 자동 사용.")]
        [SerializeField] private SpriteRenderer _fillRenderer;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.12f;

        private Color _fillBaseColor = Color.white;
        private float _flashTimer;
        private float _lastFraction = 1f;

        private void Reset()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }
            if (_fillRenderer == null && _fill != null)
            {
                _fillRenderer = _fill.GetComponentInChildren<SpriteRenderer>();
            }
            if (_fillRenderer != null)
            {
                _fillBaseColor = _fillRenderer.color;
            }
            _flashTimer = 0f;
            _lastFraction = 1f;

            if (_enemy != null)
            {
                _enemy.HealthChanged += OnHealthChanged;
            }
            SetShown(false);
        }

        private void OnDisable()
        {
            if (_enemy != null)
            {
                _enemy.HealthChanged -= OnHealthChanged;
            }
            if (_fillRenderer != null)
            {
                _fillRenderer.color = _fillBaseColor;
            }
        }

        private void Update()
        {
            if (_fillRenderer == null || _flashTimer <= 0f)
            {
                return;
            }
            _flashTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_flashTimer / Mathf.Max(0.01f, _flashDuration));
            _fillRenderer.color = Color.Lerp(_fillBaseColor, _flashColor, k);
            if (_flashTimer <= 0f)
            {
                _fillRenderer.color = _fillBaseColor;
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            float f = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (f < _lastFraction - 0.0001f)
            {
                _flashTimer = _flashDuration;
            }
            _lastFraction = f;

            if (_fill != null)
            {
                Vector3 s = _fill.localScale;
                s.x = f;
                _fill.localScale = s;
            }

            bool visible = f > 0f && (!_hideWhenFull || f < 1f);
            SetShown(visible);
        }

        private void SetShown(bool visible)
        {
            if (_barRoot != null)
            {
                _barRoot.SetActive(visible);
            }
        }
    }
}
