using UnityEngine;
using Game.Player;

namespace Game.Enemies
{
    /// <summary>
    /// 원형 파이어볼. 주어진 사각 범위 안에서 대각선으로 날며 <b>네 벽에 모두 튕긴다</b>.
    /// 플레이어에 닿으면 피해를 주되 <b>사라지지 않고 계속 튕긴다</b> (_hitCooldown 간격). 수명이 다하면 파괴.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Fireball : MonoBehaviour
    {
        [SerializeField] private float _speed = 6f;
        [SerializeField] private float _hitRadius = 0.45f;
        [SerializeField] private int _damage = 45;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private float _spin = 240f;
        [Tooltip("한 번 맞힌 뒤 다시 맞히기까지의 간격(초). 파이어볼은 사라지지 않고 계속 튕긴다.")]
        [SerializeField] private float _hitCooldown = 0.8f;

        private Vector2 _vel;
        private float _minX, _maxX, _minY, _maxY;
        private float _life;
        private float _hitCd;
        private PlayerController _pc;

        /// <param name="dir">발사 방향(정규화 안 해도 됨). 예: (-1,1)=왼쪽 위, (1,1)=오른쪽 위.</param>
        public void Launch(Vector3 pos, Vector2 dir, float minX, float maxX, float minY, float maxY)
        {
            transform.position = pos;
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
            _minY = Mathf.Min(minY, maxY);
            _maxY = Mathf.Max(minY, maxY);
            _vel = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up) * _speed;
            _life = 0f;
            _hitCd = 0f;
            _pc = PlayerController.Instance;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            Vector3 p = transform.position;
            p.x += _vel.x * Time.deltaTime;
            p.y += _vel.y * Time.deltaTime;

            if (p.x <= _minX) { p.x = _minX; _vel.x = Mathf.Abs(_vel.x); }
            else if (p.x >= _maxX) { p.x = _maxX; _vel.x = -Mathf.Abs(_vel.x); }
            if (p.y <= _minY) { p.y = _minY; _vel.y = Mathf.Abs(_vel.y); }
            else if (p.y >= _maxY) { p.y = _maxY; _vel.y = -Mathf.Abs(_vel.y); }

            transform.position = p;

            if (_hitCd > 0f)
            {
                _hitCd -= Time.deltaTime;
            }
            else if (_pc != null && _pc.IsAlive
                && (_pc.transform.position - transform.position).sqrMagnitude <= _hitRadius * _hitRadius)
            {
                _pc.Damage(_damage, transform.position);
                _hitCd = Mathf.Max(0.1f, _hitCooldown); // 사라지지 않고 계속 튕김
            }

            if (_life >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
