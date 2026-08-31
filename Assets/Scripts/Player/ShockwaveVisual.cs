using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 충격파 발동 시 원형 링이 반경까지 커지며 사라진다.
    /// _ring 은 지름 1유닛짜리 원 스프라이트 (스케일 1 오브젝트에 두는 걸 권장).
    /// </summary>
    public class ShockwaveVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _ring;
        [SerializeField] private float _duration = 0.3f;
        [Tooltip("MapGenerator 의 셀 크기와 맞춘다.")]
        [SerializeField] private float _cellSize = 1f;

        private float _t = -1f;
        private float _targetDiameter;
        private Vector3 _center;

        private void Start()
        {
            if (_ring != null)
            {
                _ring.gameObject.SetActive(false);
            }
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ShockwaveFired += OnFired;
            }
        }

        private void OnDestroy()
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ShockwaveFired -= OnFired;
            }
        }

        private void OnFired(int col, int row, int radius)
        {
            if (PlayerController.Instance != null)
            {
                _center = PlayerController.Instance.transform.position;
            }
            _targetDiameter = radius * 2f * _cellSize;
            _t = 0f;

            if (_ring != null)
            {
                _ring.gameObject.SetActive(true);
                _ring.transform.position = _center;
                _ring.transform.localScale = Vector3.one * 0.2f;
                SetAlpha(1f);
            }
        }

        private void Update()
        {
            if (_t < 0f)
            {
                return;
            }

            _t += Time.deltaTime / Mathf.Max(0.01f, _duration);
            float k = Mathf.Clamp01(_t);

            if (_ring != null)
            {
                _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, _targetDiameter, k);
                SetAlpha(1f - k);
            }

            if (_t >= 1f)
            {
                _t = -1f;
                if (_ring != null)
                {
                    _ring.gameObject.SetActive(false);
                }
            }
        }

        private void SetAlpha(float a)
        {
            Color c = _ring.color;
            c.a = a;
            _ring.color = c;
        }
    }
}
