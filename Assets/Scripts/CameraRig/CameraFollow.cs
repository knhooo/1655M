using System;
using System.Collections;
using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// 대상을 따라가되 아래쪽으로 치우쳐(플레이어가 화면 상단에 오도록) 하강이 잘 보이게 한다.
    /// 맵이 x=0 중앙 정렬이므로 x 는 고정, y 만 부드럽게 추적.
    /// 사망 시 <see cref="RewindToHome"/> 로 시작 높이까지 되감아 올라간다.
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

        /// <summary>씬 시작 시 카메라 y (되감기 목표).</summary>
        public float HomeY { get; private set; }
        public bool IsFollowing { get; private set; } = true;

        private void Start()
        {
            if (_target != null)
            {
                Vector3 p = _target.position + _offset;
                _lowestY = p.y;
                transform.position = new Vector3(_followX ? p.x : transform.position.x, p.y, _offset.z);
            }
            HomeY = transform.position.y;
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

            float y = Mathf.SmoothDamp(transform.position.y, targetY, ref _yVelocity, _smoothTime);
            float x = _followX ? desired.x : transform.position.x;

            transform.position = new Vector3(x, y, _offset.z);
        }

        /// <summary>추적을 멈추고 시작 높이(<see cref="HomeY"/>)까지 일정 속도로 되감아 올라간다.</summary>
        public void RewindToHome(float unitsPerSecond, float minDuration, float maxDuration, Action onArrived)
        {
            StopAllCoroutines();
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
