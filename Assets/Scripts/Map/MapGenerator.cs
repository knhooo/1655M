using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Game.Player;

namespace Game.Map
{
    /// <summary>
    /// 가로 9칸 고정, 세로 무한 그리드 지층 맵.
    ///
    /// 좌표계: 셀 (col, row).
    ///   - col : 0 ~ 8, 왼쪽 → 오른쪽. col 4 가 월드 x=0 (중앙).
    ///   - row : 0(지표) 부터 아래로 증가. 깊을수록 row 값이 크다. 월드 y 는 감소.
    ///
    /// 각 칸은 "지층 블록" 또는 "격자 엔티티(<see cref="GridEntity"/> = 적/아이템)" 중 하나를 담는다.
    /// 엔티티가 있는 칸은 solid 취급되고, <see cref="DamageCell"/> 은 엔티티로 전달된다.
    ///
    /// 추적 대상(_tracked)의 깊이를 기준으로 아래 행은 미리 생성하고,
    /// 화면 위로 벗어난 행은 풀에 반납한다. 플레이어는 위로 이동할 수 없으므로
    /// 회수된 행은 다시 필요해지지 않는다.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        public const int Columns = 9;

        [Header("References")]
        [SerializeField] private BlockView _blockPrefab;
        [Tooltip("이 대상의 y 위치를 기준으로 행을 스트리밍한다 (보통 플레이어).")]
        [SerializeField] private Transform _tracked;
        [Tooltip("적의 접촉 피해 판정 등에 사용. 보통 _tracked 와 같은 플레이어.")]
        [SerializeField] private PlayerController _player;
        [Tooltip("생성물의 부모이자 그리드 (col 0, row 0) 의 원점. 비우면 이 오브젝트가 기준.")]
        [SerializeField] private Transform _blockRoot;

        /// <summary>그리드 원점 + 부모. 인스펙터 미할당이어도(에디트 모드 포함) 안전하게 자기 Transform 사용.</summary>
        private Transform Root => _blockRoot != null ? _blockRoot : transform;

        public PlayerController Player => _player;

        [Header("Grid")]
        [SerializeField] private float _cellSize = 1f;
        [Tooltip("추적 대상 아래로 미리 만들어 둘 행 수.")]
        [SerializeField] private int _rowsAhead = 20;
        [Tooltip("추적 대상 위로 이만큼 벗어난 행은 회수한다.")]
        [SerializeField] private int _rowsBehind = 6;

        [Header("Strata HP (임시 값 - 나중에 StrataSO 로 대체)")]
        [SerializeField] private int _soilHp = 1;
        [SerializeField] private int _rockHp = 3;
        [SerializeField] private int _oreHp = 5;

        [Header("Strata 층 (깊이대별 단일 지층 - 임시 값)")]
        [Tooltip("맨 위 빈 지표 행 수. 이 아래부터 첫 번째 층이 시작.")]
        [SerializeField] private int _surfaceRows = 1;
        [Tooltip("위에서부터 순서대로 쌓이는 층. 각 층은 지정한 두께(행 수)만큼 그 지층으로 꽉 찬다. " +
                 "마지막 층보다 더 깊은 곳은 마지막 층 지층이 계속 이어진다.")]
        [SerializeField]
        private StrataLayer[] _layers =
        {
            new StrataLayer { type = StrataType.Soil, thickness = 10 }, // 지표~10층: 흙
            new StrataLayer { type = StrataType.Rock, thickness = 10 }, // 11~20층: 암반
            new StrataLayer { type = StrataType.Ore,  thickness = 10 }, // 21~30층: 광맥
        };

        [Serializable]
        private struct StrataLayer
        {
            public StrataType type;
            [Tooltip("이 층의 두께(행 수).")]
            public int thickness;
        }

        [Header("Entities (적 - 격자에 지층 대신 배치. 나중에 아이템도 동일 방식)")]
        [SerializeField] private int _entitySeed = 12345;
        [Tooltip("이 행부터 엔티티가 등장.")]
        [SerializeField] private int _entityStartRow = 5;
        [Tooltip("각 칸이 엔티티가 될 확률.")]
        [SerializeField, Range(0f, 0.5f)] private float _entityDensity = 0.06f;
        [Tooltip("엔티티 중 2x2 크기의 비율.")]
        [SerializeField, Range(0f, 1f)] private float _bigEntityChance = 0.25f;
        [Tooltip("1x1 적 프리팹 후보.")]
        [SerializeField] private GridEntity[] _enemyPrefabs1x1;
        [Tooltip("2x2 적 프리팹 후보. 비어 있으면 2x2 는 생성되지 않는다.")]
        [SerializeField] private GridEntity[] _enemyPrefabs2x2;

        // 이벤트 -----------------------------------------------------------
        /// <summary>블록이 완전히 파괴됐을 때: (col, row, 파괴된 블록 데이터).</summary>
        public event Action<int, int, BlockData> BlockDestroyed;
        /// <summary>엔티티가 격자에 배치된 직후. (드롭/점수 시스템이 Enemy.Killed 등을 구독하는 지점)</summary>
        public event Action<GridEntity> EntitySpawned;
        /// <summary>엔티티가 파괴로 제거될 때 (회수와 구분).</summary>
        public event Action<GridEntity> EntityCleared;

        // 상태 -----------------------------------------------------------
        private readonly Dictionary<int, MapRow> _rows = new();
        private ObjectPool<BlockView> _blockPool;

        // 엔티티 풀 (프리팹 단위)
        private readonly Dictionary<GridEntity, ObjectPool<GridEntity>> _entityPools = new();
        private readonly Dictionary<GridEntity, GridEntity> _entityPrefabOf = new();
        // 아직 생성되지 않은 아랫행에 2x2 가 미리 예약한 칸.
        private readonly Dictionary<long, GridEntity> _pendingEntityCells = new();

        private int _topRow;
        private int _bottomRow;
        private bool _initialized;

        // ------------------------------------------------------------------
        // 라이프사이클
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_tracked == null && _player != null)
            {
                _tracked = _player.transform;
            }

            int prewarm = Columns * (_rowsAhead + _rowsBehind + 4);
            _blockPool = new ObjectPool<BlockView>(
                createFunc: CreateBlockView,
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v => v.gameObject.SetActive(false),
                actionOnDestroy: v => Destroy(v.gameObject),
                collectionCheck: false,
                defaultCapacity: prewarm,
                maxSize: Columns * 512);
        }

        private void Start()
        {
            _topRow = 0;
            _bottomRow = -1;
            _initialized = true;
            StreamRows();
        }

        private void Update()
        {
            if (_initialized)
            {
                StreamRows();
            }
        }

        private BlockView CreateBlockView()
        {
            BlockView view = Instantiate(_blockPrefab, Root);
            view.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------
        // 행 스트리밍
        // ------------------------------------------------------------------

        private void StreamRows()
        {
            int focusRow = _tracked != null ? WorldToRow(_tracked.position.y) : 0;

            int wantBottom = focusRow + _rowsAhead;
            int wantTop = Mathf.Max(0, focusRow - _rowsBehind);

            for (int row = _bottomRow + 1; row <= wantBottom; row++)
            {
                BuildRow(row);
            }
            if (wantBottom > _bottomRow)
            {
                _bottomRow = wantBottom;
            }

            for (int row = _topRow; row < wantTop; row++)
            {
                ReleaseRow(row);
            }
            _topRow = wantTop;
        }

        private void BuildRow(int row)
        {
            if (_rows.ContainsKey(row))
            {
                return;
            }

            var mapRow = new MapRow();
            _rows.Add(row, mapRow);

            for (int col = 0; col < Columns; col++)
            {
                long key = CellKey(col, row);

                // 1) 위쪽 2x2 가 이 칸을 예약해 뒀나
                if (_pendingEntityCells.TryGetValue(key, out GridEntity pending))
                {
                    _pendingEntityCells.Remove(key);
                    mapRow.Entities[col] = pending;
                    continue;
                }

                // 2) 이 행에서 왼쪽 2x2 가 이미 점유
                if (mapRow.Entities[col] != null)
                {
                    continue;
                }

                // 3) 새 엔티티의 앵커?
                if (CanPlaceEntityAnchor(col, row, out GridEntity prefab, out Vector2Int size))
                {
                    SpawnEntity(prefab, size, col, row, mapRow);
                    continue;
                }

                // 4) 일반 지층
                BlockData data = GenerateBlock(col, row);
                mapRow.Cells[col] = data;
                if (data.IsSolid)
                {
                    BlockView view = _blockPool.Get();
                    view.transform.position = CellToWorld(col, row);
                    view.Bind(col, row, data);
                    mapRow.Views[col] = view;
                }
            }
        }

        private void ReleaseRow(int row)
        {
            if (!_rows.TryGetValue(row, out MapRow mapRow))
            {
                return;
            }

            for (int col = 0; col < Columns; col++)
            {
                if (mapRow.Views[col] != null)
                {
                    _blockPool.Release(mapRow.Views[col]);
                    mapRow.Views[col] = null;
                }

                GridEntity e = mapRow.Entities[col];
                if (e != null)
                {
                    mapRow.Entities[col] = null;
                    DespawnEntity(e); // 다른 활성 칸까지 정리 + 풀 반납 (idempotent)
                }
            }

            _rows.Remove(row);
        }

        // ------------------------------------------------------------------
        // 엔티티 배치
        // ------------------------------------------------------------------

        private bool CanPlaceEntityAnchor(int col, int row, out GridEntity prefab, out Vector2Int size)
        {
            prefab = null;
            size = Vector2Int.one;

            if (row < _entityStartRow)
            {
                return false;
            }
            if (Hash01(col, row, 1) >= _entityDensity)
            {
                return false;
            }

            bool big = Hash01(col, row, 2) < _bigEntityChance
                       && _enemyPrefabs2x2 != null && _enemyPrefabs2x2.Length > 0;

            if (big)
            {
                size = new Vector2Int(2, 2);
                prefab = Pick(_enemyPrefabs2x2, Hash01(col, row, 3));
            }
            else
            {
                size = Vector2Int.one;
                prefab = Pick(_enemyPrefabs1x1, Hash01(col, row, 3));
            }

            if (prefab == null || col + size.x > Columns)
            {
                return false;
            }

            // 이 행에서 가로로 자리 있는지
            MapRow mr = _rows[row];
            for (int dx = 0; dx < size.x; dx++)
            {
                if (mr.Entities[col + dx] != null)
                {
                    return false;
                }
            }
            return true;
        }

        private void SpawnEntity(GridEntity prefab, Vector2Int size, int col, int row, MapRow anchorRow)
        {
            GridEntity e = GetPool(prefab).Get();
            _entityPrefabOf[e] = prefab;

            Vector3 c0 = CellToWorld(col, row);
            Vector3 c1 = CellToWorld(col + size.x - 1, row + size.y - 1);
            e.Place(this, col, row, (c0 + c1) * 0.5f);

            for (int dy = 0; dy < size.y; dy++)
            {
                for (int dx = 0; dx < size.x; dx++)
                {
                    int cc = col + dx;
                    int rr = row + dy;

                    if (rr == row)
                    {
                        anchorRow.Entities[cc] = e;
                    }
                    else if (_rows.TryGetValue(rr, out MapRow lower))
                    {
                        lower.Entities[cc] = e;
                    }
                    else
                    {
                        _pendingEntityCells[CellKey(cc, rr)] = e;
                    }
                }
            }

            EntitySpawned?.Invoke(e);
        }

        /// <summary>엔티티가 스스로(사망 등) 제거를 요청. 파편/드롭 이벤트를 발생시킨다.</summary>
        public void ClearEntity(GridEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            EntityCleared?.Invoke(entity);
            DespawnEntity(entity);
        }

        /// <summary>풋프린트 정리 + 풀 반납. 여러 번 호출해도 안전.</summary>
        private void DespawnEntity(GridEntity entity)
        {
            if (entity == null || !_entityPrefabOf.TryGetValue(entity, out GridEntity prefab))
            {
                return;
            }
            _entityPrefabOf.Remove(entity);

            Vector2Int s = entity.Size;
            for (int dy = 0; dy < s.y; dy++)
            {
                for (int dx = 0; dx < s.x; dx++)
                {
                    int cc = entity.AnchorCol + dx;
                    int rr = entity.AnchorRow + dy;
                    if (cc >= 0 && cc < Columns
                        && _rows.TryGetValue(rr, out MapRow mr) && mr.Entities[cc] == entity)
                    {
                        mr.Entities[cc] = null;
                    }
                    _pendingEntityCells.Remove(CellKey(cc, rr));
                }
            }

            entity.Recycle();
            GetPool(prefab).Release(entity);
        }

        private ObjectPool<GridEntity> GetPool(GridEntity prefab)
        {
            if (!_entityPools.TryGetValue(prefab, out ObjectPool<GridEntity> pool))
            {
                pool = new ObjectPool<GridEntity>(
                    createFunc: () =>
                    {
                        GridEntity inst = Instantiate(prefab, Root);
                        inst.gameObject.SetActive(false);
                        return inst;
                    },
                    actionOnGet: null,
                    actionOnRelease: null,
                    actionOnDestroy: e => { if (e != null) { Destroy(e.gameObject); } },
                    collectionCheck: false,
                    defaultCapacity: 16,
                    maxSize: 256);
                _entityPools.Add(prefab, pool);
            }
            return pool;
        }

        private static GridEntity Pick(GridEntity[] arr, float t)
        {
            if (arr == null || arr.Length == 0)
            {
                return null;
            }
            int i = Mathf.Clamp(Mathf.FloorToInt(t * arr.Length), 0, arr.Length - 1);
            return arr[i];
        }

        // ------------------------------------------------------------------
        // 지층 생성 규칙 (임시)
        // ------------------------------------------------------------------

        private BlockData GenerateBlock(int col, int row)
        {
            if (row < _surfaceRows)
            {
                return BlockData.Empty;
            }

            StrataType type = LayerAt(row);
            return new BlockData(type, HpFor(type));
        }

        private StrataType LayerAt(int row)
        {
            if (_layers == null || _layers.Length == 0)
            {
                return StrataType.Soil;
            }

            int depth = row - _surfaceRows;
            int cursor = 0;
            for (int i = 0; i < _layers.Length; i++)
            {
                int thickness = Mathf.Max(0, _layers[i].thickness);
                if (depth < cursor + thickness)
                {
                    return _layers[i].type;
                }
                cursor += thickness;
            }

            return _layers[_layers.Length - 1].type;
        }

        private int HpFor(StrataType type)
        {
            switch (type)
            {
                case StrataType.Rock: return _rockHp;
                case StrataType.Ore: return _oreHp;
                default: return _soilHp;
            }
        }

        // ------------------------------------------------------------------
        // 조회 / 파괴 API
        // ------------------------------------------------------------------

        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < Columns && row >= 0;
        }

        public bool IsSolid(int col, int row)
        {
            if (col < 0 || col >= Columns)
            {
                return true; // 벽
            }
            return _rows.TryGetValue(row, out MapRow r)
                   && (r.Cells[col].IsSolid || r.Entities[col] != null);
        }

        /// <summary>이 칸에 엔티티(적/아이템)가 있으면 반환. 없으면 null.</summary>
        public GridEntity GetEntity(int col, int row)
        {
            if (col < 0 || col >= Columns)
            {
                return null;
            }
            return _rows.TryGetValue(row, out MapRow r) ? r.Entities[col] : null;
        }

        public bool IsRowReady(int row)
        {
            return _rows.ContainsKey(row);
        }

        public BlockData GetBlock(int col, int row)
        {
            if (col < 0 || col >= Columns)
            {
                return BlockData.Empty;
            }
            return _rows.TryGetValue(row, out MapRow r) ? r.Cells[col] : BlockData.Empty;
        }

        /// <summary>
        /// 특정 칸에 피해를 준다. 엔티티가 있으면 엔티티에, 없으면 지층 블록에 적용.
        /// </summary>
        /// <returns>이번 호출로 대상이 파괴됐으면 true.</returns>
        public bool DamageCell(int col, int row, int amount)
        {
            if (!InBounds(col, row) || amount <= 0)
            {
                return false;
            }
            if (!_rows.TryGetValue(row, out MapRow mapRow))
            {
                return false;
            }

            // 엔티티 우선
            GridEntity entity = mapRow.Entities[col];
            if (entity != null)
            {
                return entity.ApplyDamage(amount); // 사망 시 내부에서 ClearEntity 호출
            }

            // 지층 블록
            BlockData data = mapRow.Cells[col];
            if (!data.IsSolid)
            {
                return false;
            }

            data.Hp -= amount;

            if (data.Hp > 0)
            {
                mapRow.Cells[col] = data;
                if (mapRow.Views[col] != null)
                {
                    mapRow.Views[col].OnDamaged(data);
                }
                return false;
            }

            BlockData destroyed = data;
            mapRow.Cells[col] = BlockData.Empty;
            if (mapRow.Views[col] != null)
            {
                _blockPool.Release(mapRow.Views[col]);
                mapRow.Views[col] = null;
            }

            BlockDestroyed?.Invoke(col, row, destroyed);
            // TODO: 파편 파티클 / 사운드 / 드롭 스폰
            return true;
        }

        // ------------------------------------------------------------------
        // 좌표 변환
        // ------------------------------------------------------------------

        public Vector3 CellToWorld(int col, int row)
        {
            float x = (col - (Columns - 1) * 0.5f) * _cellSize;
            float y = -row * _cellSize;
            return Root.position + new Vector3(x, y, 0f);
        }

        public int WorldToColumn(float worldX)
        {
            float local = (worldX - Root.position.x) / _cellSize + (Columns - 1) * 0.5f;
            return Mathf.RoundToInt(local);
        }

        public int WorldToRow(float worldY)
        {
            return Mathf.RoundToInt((Root.position.y - worldY) / _cellSize);
        }

        // ------------------------------------------------------------------

        private static long CellKey(int col, int row)
        {
            return ((long)row << 20) | (uint)(col & 0xFFFFF);
        }

        private float Hash01(int col, int row, int salt)
        {
            unchecked
            {
                uint h = (uint)_entitySeed;
                h = (h ^ (uint)(col * 73856093)) * 2654435761u;
                h = (h ^ (uint)(row * 19349663)) * 2246822519u;
                h = (h ^ (uint)(salt * 83492791)) * 3266489917u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private class MapRow
        {
            public readonly BlockData[] Cells = new BlockData[Columns];
            public readonly BlockView[] Views = new BlockView[Columns];
            public readonly GridEntity[] Entities = new GridEntity[Columns];
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform root = Root;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);

            float left = -(Columns * 0.5f) * _cellSize;
            for (int c = 0; c <= Columns; c++)
            {
                float x = root.position.x + left + c * _cellSize;
                Gizmos.DrawLine(new Vector3(x, root.position.y + _cellSize, 0f),
                                new Vector3(x, root.position.y - _rowsAhead * _cellSize, 0f));
            }
        }
#endif
    }
}
