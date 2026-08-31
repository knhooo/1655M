using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 HP 바. 처음엔(풀 피) 숨겨져 있고, 한 대라도 맞으면 나타난다.
    ///
    /// 배치: <see cref="Enemy"/> 와 **같은 오브젝트**에 붙인다 (풀링 활성/비활성을 그대로 따라가도록).
    ///  - _barRoot : 배경+채움 스프라이트를 담은 자식 오브젝트. 이 스크립트가 켜고 끈다.
    ///  - _fill    : HP 비율만큼 localScale.x 가 줄어드는 채움 바.
    ///               스프라이트 피벗을 Left 로 하거나, 왼쪽 정렬된 부모 아래에 두면 왼쪽 기준으로 줄어든다.
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
        }

        private void OnHealthChanged(int current, int max)
        {
            float f = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

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
