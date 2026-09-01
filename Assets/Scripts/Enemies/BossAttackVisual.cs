using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Game.Map;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 공격 경고/강타를 격자 칸에 스프라이트로 표시한다.
    /// <see cref="MapGenerator.EntitySpawned"/> 로 <see cref="Boss"/> 를 잡아 이벤트를 구독.
    ///
    /// 배치: 항상 활성인 오브젝트에 붙이고 _map / _markerPrefab(단색 스프라이트) 연결.
    /// </summary>
    public class BossAttackVisual : MonoBehaviour
    {
        [SerializeField] private MapGenerator _map;
        [Tooltip("한 칸을 덮는 단색 스프라이트 (SpriteRenderer). 기본 비활성.")]
        [SerializeField] private SpriteRenderer _markerPrefab;
        [SerializeField] private int _prewarm = 32;

        [SerializeField] private Color _telegraphColor = new Color(1f, 0.25f, 0.15f, 0.45f);
        [SerializeField] private Color _strikeColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float _strikeFlash = 0.16f;

        private Boss _boss;
        private ObjectPool<SpriteRenderer> _pool;
        private readonly List<SpriteRenderer> _shown = new();
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
                maxSize: 128);
        }

        private void Start()
        {
            if (_map == null)
            {
                _map = FindFirstObjectByType<MapGenerator>();
            }
            if (_map != null)
            {
                _map.EntitySpawned += OnEntitySpawned;
            }
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.EntitySpawned -= OnEntitySpawned;
            }
            Unbind();
        }

        private void OnEntitySpawned(GridEntity e)
        {
            if (e is Boss boss)
            {
                Unbind();
                _boss = boss;
                _boss.AttackTelegraph += OnTelegraph;
                _boss.AttackStrike += OnStrike;
                _boss.Killed += OnKilled;
            }
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
            ClearMarkers();
        }

        private void OnKilled(int col, int row, int coin) => Unbind();

        private void OnTelegraph(IReadOnlyList<Vector2Int> cells, float duration)
        {
            if (_markerPrefab == null || _map == null)
            {
                return;
            }
            ClearMarkers();
            for (int i = 0; i < cells.Count; i++)
            {
                SpriteRenderer m = _pool.Get();
                m.transform.position = _map.CellToWorld(cells[i].x, cells[i].y);
                m.color = _telegraphColor;
                _shown.Add(m);
            }
        }

        private void OnStrike(IReadOnlyList<Vector2Int> cells)
        {
            if (_strikeCo != null)
            {
                StopCoroutine(_strikeCo);
            }
            _strikeCo = StartCoroutine(StrikeFlash());
        }

        private IEnumerator StrikeFlash()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                _shown[i].color = _strikeColor;
            }
            float t = 0f;
            float dur = Mathf.Max(0.02f, _strikeFlash);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                Color c = _strikeColor;
                c.a = _strikeColor.a * (1f - Mathf.Clamp01(t));
                for (int i = 0; i < _shown.Count; i++)
                {
                    _shown[i].color = c;
                }
                yield return null;
            }
            ClearMarkers();
            _strikeCo = null;
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
