using System.Collections;
using UnityEngine;

namespace Game.Juice
{
    /// <summary>
    /// 타격감용 순간 정지. <see cref="Do"/> 로 짧게 <see cref="Time.timeScale"/> 를 0 으로 떨군다.
    /// 씬에 하나만 두면 됨 (항상 활성인 오브젝트). 겹친 요청은 더 늦은 복귀 시각을 채택.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Tooltip("정지 해제 후 되돌릴 시간 배율.")]
        [SerializeField] private float _defaultTimeScale = 1f;
        [Tooltip("한 번의 요청이 멈출 수 있는 최대 시간(초). 안전장치.")]
        [SerializeField] private float _maxDuration = 0.3f;

        private float _resumeAtUnscaled = -1f;
        private bool _frozen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Restore();
        }

        private void OnDisable() => Restore();

        /// <summary><paramref name="seconds"/>(실시간) 동안 게임을 멈춘다.</summary>
        public static void Do(float seconds)
        {
            if (Instance != null)
            {
                Instance.Freeze(seconds);
            }
        }

        private void Freeze(float seconds)
        {
            seconds = Mathf.Clamp(seconds, 0f, _maxDuration);
            if (seconds <= 0f)
            {
                return;
            }

            _resumeAtUnscaled = Mathf.Max(_resumeAtUnscaled, Time.unscaledTime + seconds);

            if (!_frozen)
            {
                _frozen = true;
                Time.timeScale = 0f;
                StartCoroutine(UnfreezeRoutine());
            }
        }

        private IEnumerator UnfreezeRoutine()
        {
            while (Time.unscaledTime < _resumeAtUnscaled)
            {
                yield return null;
            }
            Time.timeScale = _defaultTimeScale;
            _frozen = false;
            _resumeAtUnscaled = -1f;
        }

        private void Restore()
        {
            if (_frozen)
            {
                Time.timeScale = _defaultTimeScale;
                _frozen = false;
                _resumeAtUnscaled = -1f;
            }
        }
    }
}
