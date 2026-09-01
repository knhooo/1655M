using UnityEngine;
using Game.Player;

namespace Game.Enemies
{
    /// <summary>
    /// 원형 파이어볼. 주어진 사각 범위 안에서 대각선으로 날며 <b>네 벽에 모두 튕긴다</b>.
    /// 플레이어에 닿으면 피해를 주되 <b>사라지지 않고 계속 튕긴다</b> (_hitCooldown 간격). 수명이 다하면 파괴.
    /// <para>
    /// <see cref="Launch"/> 에 <c>bounceJitter</c> 를 주면 벽에 튕길 때마다 반사각을 그만큼(±도) 흔든다.
    /// 보스 B 페이즈 2에서 궤도 예측을 어렵게 만드는 용도.
    /// </para>
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
        [Tooltip("반사 후 진행각이 벽에 이 각도(도)보다 얕으면 밀어낸다. 벽에 붙어 왕복하는 지루함 방지.")]
        [SerializeField] private float _minBounceAngle = 12f;

        private Vector2 _vel;
        private float _minX, _maxX, _minY, _maxY;
        private float _life;
        private float _hitCd;
        private float _bounceJitter;   // 벽 반사마다 ±이 각도(도) 만큼 랜덤 회전
        private PlayerController _pc;

        /// <param name="dir">발사 방향(정규화 안 해도 됨). 예: (-1,1)=왼쪽 위, (1,1)=오른쪽 위.</param>
        /// <param name="bounceJitter">벽 반사 시 반사각 랜덤 흔들림(±도). 0 = 정확 반사.</param>
        public void Launch(Vector3 pos, Vector2 dir, float minX, float maxX, float minY, float maxY, float bounceJitter = 0f)
        {
            transform.position = pos;
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
            _minY = Mathf.Min(minY, maxY);
            _maxY = Mathf.Max(minY, maxY);
            _vel = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up) * _speed;
            _life = 0f;
            _hitCd = 0f;
            _bounceJitter = Mathf.Max(0f, bounceJitter);
            _pc = PlayerController.Instance;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            Vector3 p = transform.position;
            p.x += _vel.x * Time.deltaTime;
            p.y += _vel.y * Time.deltaTime;

            if (p.x <= _minX) { p.x = _minX; _vel.x = Mathf.Abs(_vel.x); OnBounce(true); }
            else if (p.x >= _maxX) { p.x = _maxX; _vel.x = -Mathf.Abs(_vel.x); OnBounce(true); }
            if (p.y <= _minY) { p.y = _minY; _vel.y = Mathf.Abs(_vel.y); OnBounce(false); }
            else if (p.y >= _maxY) { p.y = _maxY; _vel.y = -Mathf.Abs(_vel.y); OnBounce(false); }

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

        /// <param name="hitVerticalWall">true = 좌/우 벽에 튕김(x 반사), false = 상/하 벽(y 반사).</param>
        private void OnBounce(bool hitVerticalWall)
        {
            // 방금 반사된 축의 부호(벽 안쪽으로 다시 들어가면 안 됨)
            float keepX = Mathf.Sign(_vel.x == 0f ? 1f : _vel.x);
            float keepY = Mathf.Sign(_vel.y == 0f ? 1f : _vel.y);

            if (_bounceJitter > 0f)
            {
                float a = Random.Range(-_bounceJitter, _bounceJitter) * Mathf.Deg2Rad;
                float cs = Mathf.Cos(a);
                float sn = Mathf.Sin(a);
                _vel = new Vector2(_vel.x * cs - _vel.y * sn, _vel.x * sn + _vel.y * cs);
            }

            // 얕은 반사각 밀어내기 + 반사축 부호 복구
            float minA = Mathf.Max(0f, _minBounceAngle) * Mathf.Deg2Rad;
            float ax = Mathf.Abs(_vel.x);
            float ay = Mathf.Abs(_vel.y);
            float mag = Mathf.Max(0.001f, _vel.magnitude);

            if (hitVerticalWall)
            {
                float minAx = mag * Mathf.Sin(minA); // x 성분(벽에서 멀어지는 방향)이 최소 이만큼
                if (ax < minAx || Mathf.Sign(_vel.x == 0f ? keepX : _vel.x) != keepX)
                {
                    ax = Mathf.Max(ax, minAx);
                }
                ay = Mathf.Sqrt(Mathf.Max(0f, mag * mag - ax * ax));
                _vel = new Vector2(keepX * ax, Mathf.Sign(_vel.y == 0f ? (Random.value < 0.5f ? -1f : 1f) : _vel.y) * ay);
            }
            else
            {
                float minAy = mag * Mathf.Sin(minA);
                if (ay < minAy || Mathf.Sign(_vel.y == 0f ? keepY : _vel.y) != keepY)
                {
                    ay = Mathf.Max(ay, minAy);
                }
                ax = Mathf.Sqrt(Mathf.Max(0f, mag * mag - ay * ay));
                _vel = new Vector2(Mathf.Sign(_vel.x == 0f ? (Random.value < 0.5f ? -1f : 1f) : _vel.x) * ax, keepY * ay);
            }

            _vel = _vel.normalized * _speed; // 속력 고정
        }
    }
}
