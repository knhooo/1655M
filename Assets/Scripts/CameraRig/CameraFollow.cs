using System;
using System.Collections;
using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// 대상을 따라가되 아래쪽으로 치우쳐(플레이어가 화면 상단에 오도록) 하강이 잘 보이게 한다.
    /// 맵이 x=0 중앙 정렬이므로 x 는 고정, y 만 부드럽게 추적.
    /// 사망 시 <see cref="RewindToHome"/> 로 시작 높이까지 되감아 올라간다.
    /// <see cref="Shake"/> 로 타격감용 흔들림을 준다 (추적 위치에 오프셋만 더함).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("대상 기준 오프셋. y 를 음수로 두면 대상이 화면 위쪽에 위치.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, -2.5f, -10f);

        [Tooltip("y 추적 부드러움(작을수록 빠름).")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Tooltip("x 도 대상을 따라갈지. 맵이 중앙 정렬이면 꺼둔다.")]
        [SerializeField] private bool _followX = false;

        [Tooltip("한 번 내려간 높이 위로는 다시 올라가지 않음(카메라가 되돌아가지 않게).")]
        [SerializeField] private bool _descendOnly = true;

        private float _yVelocity;
        private float _lowestY = float.PositiveInfinity;

        // 추적 위치(흔들림 오프셋 제외)
        private float _curX;
        private float _curY;

        // 흔들림
        [Tooltip("흔들림 진동수(Hz). 낮을수록 부드러운 '쿵', 높을수록 '지지직'.")]
        [SerializeField] private float _shakeFrequency = 14f;
        private float _shakeAmp;
        private float _shakeDur;
        private float _shakeElapsed;
        private Vector2 _shakeOffset;
        private float _shakeSeedX;
        private float _shakeSeedY;

        /// <summary>씬 시작 시 카메라 y (되감기 목표).</summary>
        public float HomeY { get; private set; }
        public bool IsFollowing { get; private set; } = true;

        private void Start()
        {
            _curX = transform.position.x;
            _curY = transform.position.y;

            if (_target != null)
            {
                Vector3 p = _target.position + _offset;
                _lowestY = p.y;
                _curY = p.y;
                if (_followX)
                {
                    _curX = p.x;
                }
            }

            transform.position = new Vector3(_curX, _curY, _offset.z);
            HomeY = _curY;
        }

        private void LateUpdate()
        {
            if (!IsFollowing || _target == null)
            {
                return;
            }

            Vector3 desired = _target.position + _offset;

            float targetY = desired.y;
            if (_descendOnly)
            {
                _lowestY = Mathf.Min(_lowestY, targetY);
                targetY = _lowestY;
            }

            _curY = Mathf.SmoothDamp(_curY, targetY, ref _yVelocity, _smoothTime);
            if (_followX)
            {
                _curX = desired.x;
            }

            TickShake();

            transform.position = new Vector3(
                _curX + _shakeOffset.x,
                _curY + _shakeOffset.y,
                _offset.z);
        }

        // ------------------------------------------------------------------
        // 흔들림
        // ------------------------------------------------------------------

        /// <summary>
        /// 카메라를 <paramref name="amplitude"/> 만큼(월드 단위) <paramref name="duration"/> 초 동안 흔든다.
        /// 진행 중인 흔들림보다 약하면 무시(더 센 타격이 우선).
        /// </summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f)
            {
                return;
            }
            bool finished = _shakeElapsed >= _shakeDur;
            if (finished || amplitude >= _shakeAmp)
            {
                _shakeAmp = amplitude;
                _shakeDur = duration;
                _shakeElapsed = 0f;
                _shakeSeedX = UnityEngine.Random.value * 100f;
                _shakeSeedY = UnityEngine.Random.value * 100f;
            }
        }

        private void TickShake()
        {
            if (_shakeElapsed >= _shakeDur)
            {
                _shakeOffset = Vector2.zero;
                return;
            }
            _shakeElapsed += Time.unscaledDeltaTime; // 히트스톱 중에도 흔들림은 진행
            float k = 1f - Mathf.Clamp01(_shakeElapsed / _shakeDur);
            float mag = _shakeAmp * k * k; // 빠르게 감쇠

            // 프레임마다 랜덤(지지직) 대신 Perlin 노이즈로 부드럽게
            float t = _shakeElapsed * _shakeFrequency;
            float nx = Mathf.PerlinNoise(_shakeSeedX, t) - 0.5f;
            float ny = Mathf.PerlinNoise(_shakeSeedY, t) - 0.5f;
            _shakeOffset = new Vector2(nx, ny) * (2f * mag);
        }

        // ------------------------------------------------------------------
        // 되감기
        // ------------------------------------------------------------------

        /// <summary>추적을 멈추고 시작 높이(<see cref="HomeY"/>)까지 일정 속도로 되감아 올라간다.</summary>
        public void RewindToHome(float unitsPerSecond, float minDuration, float maxDuration, Action onArrived)
        {
            StopAllCoroutines();
            _shakeElapsed = _shakeDur;
            _shakeOffset = Vector2.zero;
            StartCoroutine(RewindRoutine(HomeY, unitsPerSecond, minDuration, maxDuration, onArrived));
        }

        private IEnumerator RewindRoutine(float targetY, float speed, float minDuration, float maxDuration, Action onArrived)
        {
            IsFollowing = false;

            float fromY = transform.position.y;
            float distance = Mathf.Abs(targetY - fromY);
            float duration = Mathf.Clamp(distance / Mathf.Max(0.01f, speed), minDuration, Mathf.Max(minDuration, maxDuration));

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                Vector3 p = transform.position;
                transform.position = new Vector3(p.x, Mathf.Lerp(fromY, targetY, k), _offset.z);
                yield return null;
            }

            Vector3 f = transform.position;
            transform.position = new Vector3(f.x, targetY, _offset.z);
            onArrived?.Invoke();
        }
    }
}
