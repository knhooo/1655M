using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 사망 연출: 위로 튕겼다가 중력으로 낙하 + 회전. 화면 밖으로 나가면 콜백(풀 반납).
    /// 적 프리팹에 붙이고 <see cref="Enemy"/> 의 _deathToss 에 연결한다.
    /// 격자에서 떼어낸(<see cref="Game.Map.MapGenerator.DetachEntity"/>) 뒤에 <see cref="Play"/> 호출.
    /// </summary>
    public class DeathToss : MonoBehaviour
    {
        [SerializeField] private float _upSpeedMin = 4f;
        [SerializeField] private float _upSpeedMax = 7.5f;
        [SerializeField] private float _sideSpeed = 3f;
        [SerializeField] private float _gravity = 22f;
        [SerializeField] private float _spinSpeed = 420f;
        [Tooltip("이 시간이 지나면 화면 안이어도 강제 종료(안전장치).")]
        [SerializeField] private float _maxLifetime = 3f;

        private Camera _cam;
        private Vector3 _velocity;
        private float _spin;
        private float _life;
        private Action _onDone;
        private bool _playing;

        public void Play(Action onDone)
        {
            enabled = true; // 컴포넌트가 꺼져 있어도 강제로
            _onDone = onDone;
            if (_cam == null)
            {
                _cam = Camera.main;
            }
            _velocity = new Vector3(
                UnityEngine.Random.Range(-_sideSpeed, _sideSpeed),
                UnityEngine.Random.Range(_upSpeedMin, _upSpeedMax),
                0f);
            _spin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * _spinSpeed;
            _life = 0f;
            _playing = true;
        }

        private void OnDisable()
        {
            // 풀 반납 등으로 비활성화되면 즉시 중단 (콜백은 이미 불렸거나 필요 없음)
            _playing = false;
            _onDone = null;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _life += Time.deltaTime;
            _velocity.y -= _gravity * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            if (_life >= _maxLifetime || IsOffScreen())
            {
                _playing = false;
                transform.rotation = Quaternion.identity; // 풀 복귀 전 정리
                Action cb = _onDone;
                _onDone = null;
                cb?.Invoke();
            }
        }

        private bool IsOffScreen()
        {
            if (_cam == null)
            {
                return false;
            }
            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            return vp.z <= 0f || vp.y < -0.2f || vp.x < -0.25f || vp.x > 1.25f;
        }
    }
}
