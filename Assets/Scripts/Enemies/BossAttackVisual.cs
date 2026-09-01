using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Game.Map;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 공격 경고/강타를 격자 칸에 스프라이트로 표시한다. 경고 동안 깜빡이며(끝에 가까울수록 빠르게)
    /// 강타 시 흰색으로 번쩍 → 페이드.
    ///
    /// 배치 2가지 중 택1:
    ///  A) <b>보스 프리팹의 자식</b>에 붙이면 부모의 <see cref="Boss"/> 를 자동으로 잡는다 (권장).
    ///  B) 씬의 항상 활성 오브젝트에 붙이면 <see cref="MapGenerator.EntitySpawned"/> 로 보스를 감지한다.
    /// _markerPrefab(단색 사각 스프라이트)만 연결하면 됨. _map 은 비워도 됨.
    /// </summary>
    public class BossAttackVisual : MonoBehaviour
    {
        [SerializeField] private MapGenerator _map;
        [Tooltip("한 칸을 덮는 단색 스프라이트 (SpriteRenderer). 기본 비활성.")]
        [SerializeField] private SpriteRenderer _markerPrefab;
        [SerializeField] private int _prewarm = 48;

        [Header("모양")]
        [Tooltip("셀 크기 대비 마커 크기.")]
        [SerializeField] private float _markerScale = 0.95f;
        [Tooltip("마커 sorting order — 블록/보스보다 위로.")]
        [SerializeField] private int _sortingOrder = 200;

        [Header("색")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.15f, 0.1f, 0.7f);
        [SerializeField] private Color _strikeColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float _strikeFlash = 0.18f;

        [Header("깜빡임 (경고 동안)")]
        [SerializeField] private float _blinkHzStart = 2f;
        [SerializeField] private float _blinkHzEnd = 9f;

        [Header("강타 파동 (원형 충격파가 라인 따라 연속으로 터짐)")]
        [Tooltip("원형 스프라이트. 비우면 마커 흰색 플래시로 대체.")]
        [SerializeField] private SpriteRenderer _shockPrefab;
        [Tooltip("칸마다 충격파가 터지는 간격(초). 가로=왼→오, 세로=아래→위.")]
        [SerializeField] private float _rippleInterval = 0.04f;
        [SerializeField] private float _shockDuration = 0.35f;
        [SerializeField] private float _shockStartScale = 0.3f;
        [SerializeField] private float _shockEndScale = 1.7f;
        [SerializeField] private Color _shockColor = new Color(1f, 0.55f, 0.2f, 0.95f);

        private static readonly System.Comparison<Vector2Int> _byXAsc = (a, b) => a.x.CompareTo(b.x);
        private static readonly System.Comparison<Vector2Int> _byYDesc = (a, b) => b.y.CompareTo(a.y);

        private Boss _boss;
        private ObjectPool<SpriteRenderer> _pool;
        private ObjectPool<SpriteRenderer> _shockPool;
        private readonly List<SpriteRenderer> _shown = new();
        private readonly List<Vector2Int> _strikeOrder = new();
        private Coroutine _telegraphCo;
        private Coroutine _strikeCo;

        private void Awake()
        {
            _pool = new ObjectPool<SpriteRenderer>(
                createFunc: () =>
                {
                    SpriteRenderer m = Instantiate(_markerPrefab, transform);
                    m.gameObject.SetActive(false);
                    return m;
                },
                actionOnGet: m => m.gameObject.SetActive(true),
                actionOnRelease: m => m.gameObject.SetActive(false),
                actionOnDestroy: m => { if (m != null) Destroy(m.gameObject); },
                collectionCheck: false,
                defaultCapacity: _prewarm,
                maxSize: 200);

            if (_shockPrefab != null)
            {
                _shockPool = new ObjectPool<SpriteRenderer>(
                    createFunc: () =>
                    {
                        SpriteRenderer s = Instantiate(_shockPrefab, transform);
                        s.gameObject.SetActive(false);
                        return s;
                    },
                    actionOnGet: s => s.gameObject.SetActive(true),
                    actionOnRelease: s => s.gameObject.SetActive(false),
                    actionOnDestroy: s => { if (s != null) Destroy(s.gameObject); },
                    collectionCheck: false,
                    defaultCapacity: 16,
                    maxSize: 64);
            }
        }

        private bool _subscribed;

        private void OnEnable()
        {
            if (_map == null)
            {
                _map = MapGenerator.Instance;
            }
            // A) 부모에 Boss 가 있으면 즉시 직접 연결 (프리팹 자식 배치)
            Boss parentBoss = GetComponentInParent<Boss>();
            if (parentBoss != null)
            {
                Bind(parentBoss);
            }
        }

        private void Start()
        {
            if (_markerPrefab == null)
            {
                Debug.LogWarning("[BossAttackVisual] _markerPrefab 미할당 — 경고 마커 안 뜸.", this);
            }
            if (_boss != null)
            {
                return; // 이미 부모 보스에 연결됨
            }

            // B) 씬 모드 — 모든 Awake 이후(Start)에 EntitySpawned 구독
            if (_map == null)
            {
                _map = MapGenerator.Instance;
            }
            if (_map != null)
            {
                _map.EntitySpawned += OnEntitySpawned;
                _subscribed = true;
            }
            else
            {
                Debug.LogWarning("[BossAttackVisual] MapGenerator 를 찾지 못함 — 경고 마커 안 뜸.", this);
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            if (_subscribed && _map != null)
            {
                _map.EntitySpawned -= OnEntitySpawned;
                _subscribed = false;
            }
        }

        private void OnEntitySpawned(GridEntity e)
        {
            if (e is Boss boss)
            {
                Bind(boss);
            }
        }

        private void Bind(Boss boss)
        {
            Unbind();
            _boss = boss;
            _boss.AttackTelegraph += OnTelegraph;
            _boss.AttackStrike += OnStrike;
            _boss.Killed += OnKilled;
            Debug.Log($"[BossAttackVisual] 보스 연결됨: {boss.GetType().Name}", this);
        }

        private void Unbind()
        {
            if (_boss == null)
            {
                return;
            }
            _boss.AttackTelegraph -= OnTelegraph;
            _boss.AttackStrike -= OnStrike;
            _boss.Killed -= OnKilled;
            _boss = null;
            StopAll();
            ClearMarkers();
        }

        private void OnKilled(int col, int row, int coin) => Unbind();

        private void OnTelegraph(IReadOnlyList<Vector2Int> cells, float duration)
        {
            Debug.Log($"[BossAttackVisual] OnTelegraph cells={cells.Count} prefab={( _markerPrefab != null)} map={(_map != null)}", this);
            if (_markerPrefab == null || _map == null)
            {
                return;
            }

            StopAll();
            ClearMarkers();

            float s = Mathf.Max(0.05f, _markerScale) * _map.CellSize;
            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer m = _pool.Get();
                m.transform.position = _map.CellToWorld(cells[i].x, cells[i].y);
                m.transform.localScale = new Vector3(s, s, 1f);
                m.sortingOrder = _sortingOrder;
                m.color = _telegraphColor;
                _shown.Add(m);
            }

            _telegraphCo = StartCoroutine(TelegraphBlink(Mathf.Max(0.05f, duration)));
        }

        private IEnumerator TelegraphBlink(float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float hz = Mathf.Lerp(_blinkHzStart, _blinkHzEnd, k);
                float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * hz * Mathf.PI * 2f));
                SetAlpha(_telegraphColor, _telegraphColor.a * pulse);
                yield return null;
            }
            SetAlpha(_telegraphColor, _telegraphColor.a);
            _telegraphCo = null;
        }

        private void OnStrike(IReadOnlyList<Vector2Int> cells)
        {
            if (_telegraphCo != null)
            {
                StopCoroutine(_telegraphCo);
                _telegraphCo = null;
            }
            if (_strikeCo != null)
            {
                StopCoroutine(_strikeCo);
            }

            // 라인 방향으로 순서 정하기: 가로=왼→오, 세로=아래(큰 y)→위
            _strikeOrder.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                _strikeOrder.Add(cells[i]);
            }
            OrderAlongLine(_strikeOrder);

            _strikeCo = StartCoroutine(RippleRoutine());
        }

        private static void OrderAlongLine(List<Vector2Int> list)
        {
            if (list.Count < 2)
            {
                return;
            }
            bool sameY = true, sameX = true;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].y != list[0].y) sameY = false;
                if (list[i].x != list[0].x) sameX = false;
            }
            if (sameY) list.Sort(_byXAsc);
            else if (sameX) list.Sort(_byYDesc);
        }

        private IEnumerator RippleRoutine()
        {
            // 마커는 흰색으로 확 밝힌 뒤 곧 정리
            SetColor(_strikeColor);

            if (_shockPool != null && _map != null)
            {
                var wait = new WaitForSeconds(Mathf.Max(0f, _rippleInterval));
                for (int i = 0; i < _strikeOrder.Count; i++)
                {
                    SpawnShock(_map.CellToWorld(_strikeOrder[i].x, _strikeOrder[i].y));
                    if (_rippleInterval > 0f)
                    {
                        yield return wait;
                    }
                }
                yield return new WaitForSeconds(_shockDuration);
            }
            else
            {
                // 폴백: 마커 흰색 플래시
                float t = 0f;
                float dur = Mathf.Max(0.02f, _strikeFlash);
                while (t < 1f)
                {
                    t += Time.deltaTime / dur;
                    SetAlpha(_strikeColor, _strikeColor.a * (1f - Mathf.Clamp01(t)));
                    yield return null;
                }
            }

            ClearMarkers();
            _strikeCo = null;
        }

        private void SpawnShock(Vector3 pos)
        {
            SpriteRenderer s = _shockPool.Get();
            s.transform.position = pos;
            s.sortingOrder = _sortingOrder + 1;
            StartCoroutine(ShockAnim(s));
        }

        private IEnumerator ShockAnim(SpriteRenderer s)
        {
            float cell = _map != null ? _map.CellSize : 1f;
            float t = 0f;
            float dur = Mathf.Max(0.02f, _shockDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);
                float scale = Mathf.Lerp(_shockStartScale, _shockEndScale, k) * cell;
                s.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = _shockColor;
                c.a = _shockColor.a * (1f - k);
                s.color = c;
                yield return null;
            }
            _shockPool.Release(s);
        }

        private void SetColor(Color c)
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i] != null) _shown[i].color = c;
            }
        }

        private void SetAlpha(Color baseColor, float a)
        {
            Color c = baseColor;
            c.a = a;
            SetColor(c);
        }

        private void StopAll()
        {
            if (_telegraphCo != null) { StopCoroutine(_telegraphCo); _telegraphCo = null; }
            if (_strikeCo != null) { StopCoroutine(_strikeCo); _strikeCo = null; }
        }

        private void ClearMarkers()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i] != null)
                {
                    _pool.Release(_shown[i]);
                }
            }
            _shown.Clear();
        }
    }
}
