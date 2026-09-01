using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 잠깐 나타나는 연출 오브젝트. <b>작게 시작 → 커졌다가 → 작아지며 사라진다</b> (+선택: 스핀, 페이드).
    /// 활성화되면 자동으로 <see cref="_lifetime"/> 만큼 재생 후 스스로 파괴. 호출자가 <see cref="Play"/> 로
    /// 재생 시간을 지정할 수도 있다.
    /// </summary>
    public class TransientVfx : MonoBehaviour
    {
        [SerializeField] private float _lifetime = 3f;
        [SerializeField] private float _peakScale = 1f;
        [Tooltip("전체 시간 중 최대 크기에 도달하는 시점(0~1). 낮을수록 빨리 커짐.")]
        [SerializeField, Range(0.05f, 0.9f)] private float _peakAt = 0.3f;
        [SerializeField] private float _spin = 180f;
        [Tooltip("선택. 있으면 후반부에 알파 페이드.")]
        [SerializeField] private SpriteRenderer _fadeRenderer;

        private float _t;
        private float _dur;
        private float _peakT;
        private bool _playing;
        private Color _baseColor = Color.white;

        private void OnEnable()
        {
            if (!_playing)
            {
                Play(_lifetime);
            }
        }

        /// <summary>재생 시작(또는 재시작). <paramref name="lifetime"/> 후 파괴.</summary>
        public void Play(float lifetime)
        {
            _dur = Mathf.Max(0.1f, lifetime);
            _peakT = Mathf.Clamp01(_peakAt) * _dur;
            _t = 0f;
            _playing = true;
            if (_fadeRenderer != null)
            {
                _baseColor = _fadeRenderer.color;
            }
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }
            _t += Time.deltaTime;

            float s = _t <= _peakT
                ? Mathf.SmoothStep(0f, _peakScale, _t / Mathf.Max(0.01f, _peakT))
                : Mathf.SmoothStep(_peakScale, 0f, (_t - _peakT) / Mathf.Max(0.01f, _dur - _peakT));
            transform.localScale = new Vector3(s, s, 1f);

            if (_spin != 0f)
            {
                transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            }

            if (_fadeRenderer != null)
            {
                Color c = _baseColor;
                c.a = _baseColor.a * Mathf.Clamp01(1f - _t / _dur);
                _fadeRenderer.color = c;
            }

            if (_t >= _dur)
            {
                Destroy(gameObject);
            }
        }
    }
}
