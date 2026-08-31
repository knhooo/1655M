using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Game.Player;
using Game.Enemies;

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
        [Tooltip("스트리밍 기준 대상. 비우면 PlayerController.Instance 사용.")]
        [SerializeField] private Transform _tracked;
        [Tooltip("생성물의 부모이자 그리드 (col 0, row 0) 의 원점. 비우면 이 오브젝트가 기준.")]
        [SerializeField] private Transform _blockRoot;

        /// <summary>그리드 원점 + 부모. 인스펙터 미할당이어도(에디트 모드 포함) 안전하게 자기 Transform 사용.</summary>
        private Transform Root => _blockRoot != null ? _blockRoot : transform;

        public PlayerController Player => PlayerController.Instance;

        /// <summary>스트리밍 기준 Transform. _tracked 미할당이면 플레이어.</summary>
        private Transform Tracked => _tracked != null ? _tracked
            : (PlayerController.Instance != null ? PlayerController.Instance.transform : null);

        [Header("Grid")]
        [SerializeField] private float _cellSize = 1f;
        [Tooltip("추적 대상 아래로 미리 만들어 둘 행 수.")]
        [SerializeField] private int _rowsAhead = 20;
        [Tooltip("추적 대상 위로 이만큼 벗어난 행은 회수한다.")]
        [SerializeField] private int _rowsBehind = 6;

        [Header("Strata HP (임시 밸런스 - 플레이어 대미지 17 기준: 1 / 3 / 6 타)")]
        [SerializeField] private int _soilHp = 15;
        [SerializeField] private int _rockHp = 45;
        [SerializeField] private int _oreHp = 95;

        [Header("Strata 접촉 피해 (지층 = 기본 적. 인접 시 플레이어가 받는 피해)")]
        [SerializeField] private int _soilContactDamage = 5;
        [SerializeField] private int _rockContactDamage = 16;
        [SerializeField] private int _oreContactDamage = 27;

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

        [Header("Entities (적·상자·아이템 - 격자에 지층 대신 배치)")]
        [SerializeField] private int _entitySeed = 12345;
        [Tooltip("이 행부터 엔티티가 등장.")]
        [SerializeField] private int _entityStartRow = 5;
        [Tooltip("각 칸이 엔티티가 될 확률.")]
        [SerializeField, Range(0f, 0.5f)] private float _entityDensity = 0.08f;
        [Tooltip("스폰 후보 가중치 테이블. 점유 칸 크기는 프리팹의 Size 를 따른다.")]
        [SerializeField] private EntitySpawn[] _entityTable;

        [Serializable]
        private class EntitySpawn
        {
            public GridEntity prefab;
            [Min(0f)] public float weight = 1f;
            [Tooltip("이 행 이상에서만 등장 (0 = 제한 없음).")]
            public int minRow = 0;
        }

        // ---- 적 편대 (같은 적을 정해진 모양으로 무리 배치) ----

        public enum FormationShape
        {
            Line,        // 가로 일자
            Pyramid,     // 위가 뾰족한 삼각형 (span 은 밑변)
            ChestGuard,  // 3x3, 가운데 상자 + 둘레 8칸 적
        }

        [Serializable]
        private class FormationSpawn
        {
            public FormationShape shape = FormationShape.Line;
            [Tooltip("무리를 이루는 적 (보통 enemy_small).")]
            public Enemy enemy;
            [Tooltip("ChestGuard 전용: 가운데에 놓을 상자 프리팹 (루트에 GridEntity 필요).")]
            public GameObject chest;
            [Tooltip("Line 길이 / Pyramid 밑변. ChestGuard 는 무시.")]
            [Min(1)] public int span = 5;
            [Min(0f)] public float weight = 1f;
            [Tooltip("이 행 이상에서만 등장.")]
            public int minRow = 10;
        }

        [Tooltip("각 칸이 편대 앵커가 될 확률. 낮게 유지.")]
        [SerializeField, Range(0f, 0.1f)] private float _formationDensity = 0.015f;
        [SerializeField] private FormationSpawn[] _formationTable;

        // 이벤트 -----------------------------------------------------------
        /// <summary>블록이 완전히 파괴됐을 때: (col, row, 파괴된 블록 데이터).</summary>
        public event Action<int, int, BlockData> BlockDestroyed;
        /// <summary>엔티티가 격자에 배치된 직후. (드롭/점수 시스템이 Enemy.Killed 등을 구독하는 지점)</summary>
        public event Action<GridEntity> EntitySpawned;
        /// <summary>엔티티가 파괴로 제거될 때 (회수와 구분).</summary>
        public event Action<GridEntity> EntityCleared;
        /// <summary>한 칸이 피해를 받았을 때: (월드 위치, 피해량, 치명타 여부, 엔티티인가). 데미지 텍스트용.</summary>
        public event Action<Vector3, int, bool, bool> DamageDealt;

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
        private bool _streaming = true;

        // ------------------------------------------------------------------
        // 라이프사이클
        // ------------------------------------------------------------------

        private void Awake()
        {
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
            if (_initialized && _streaming)
            {
                StreamRows();
            }
        }

        /// <summary>사망 되감기 중 등, 행 생성/회수를 잠시 멈춘다.</summary>
        public void SetStreamingEnabled(bool enabled)
        {
            _streaming = enabled;
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
            Transform tracked = Tracked;
            int focusRow = tracked != null ? WorldToRow(tracked.position.y) : 0;

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

                // 2.5) 적 편대 앵커?
                if (TryPlaceFormation(col, row, mapRow))
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

            if (row < _entityStartRow || _entityTable == null || _entityTable.Length == 0)
            {
                return false;
            }
            if (Hash01(col, row, 1) >= _entityDensity)
            {
                return false;
            }

            prefab = PickWeighted(row, Hash01(col, row, 2));
            if (prefab == null)
            {
                return false;
            }
            size = prefab.Size;

            if (col + size.x > Columns)
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

        /// <summary>깊이 조건을 만족하는 테이블 항목 중 가중치로 하나 선택.</summary>
        private GridEntity PickWeighted(int row, float t)
        {
            float total = 0f;
            for (int i = 0; i < _entityTable.Length; i++)
            {
                EntitySpawn e = _entityTable[i];
                if (e != null && e.prefab != null && row >= e.minRow)
                {
                    total += Mathf.Max(0f, e.weight);
                }
            }
            if (total <= 0f)
            {
                return null;
            }

            float pick = Mathf.Clamp01(t) * total;
            float acc = 0f;
            for (int i = 0; i < _entityTable.Length; i++)
            {
                EntitySpawn e = _entityTable[i];
                if (e == null || e.prefab == null || row < e.minRow)
                {
                    continue;
                }
                acc += Mathf.Max(0f, e.weight);
                if (pick <= acc)
                {
                    return e.prefab;
                }
            }
            return null;
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

        // ------------------------------------------------------------------
        // 적 편대 (같은 적을 정해진 모양으로 무리 배치)
        // ------------------------------------------------------------------

        private readonly List<(int col, int row, GridEntity prefab)> _formationBuf = new();

        private bool TryPlaceFormation(int col, int row, MapRow mapRow)
        {
            if (_formationTable == null || _formationTable.Length == 0)
            {
                return false;
            }
            if (Hash01(col, row, 7) >= _formationDensity)
            {
                return false;
            }

            FormationSpawn f = PickFormation(row, Hash01(col, row, 8));
            if (f == null || f.enemy == null)
            {
                return false;
            }

            _formationBuf.Clear();
            if (!BuildFormationCells(f, col, row, _formationBuf))
            {
                return false;
            }

            // 이미 예약/점유된 칸과 겹치면 포기 (부분 배치 방지)
            foreach (var (cc, rr, _) in _formationBuf)
            {
                if (cc < 0 || cc >= Columns || rr < 0)
                {
                    return false;
                }
                if (_pendingEntityCells.ContainsKey(CellKey(cc, rr)))
                {
                    return false;
                }
                if (rr == row && mapRow.Entities[cc] != null)
                {
                    return false;
                }
                if (rr != row && _rows.TryGetValue(rr, out MapRow other) && other.Entities[cc] != null)
                {
                    return false;
                }
            }

            foreach (var (cc, rr, prefab) in _formationBuf)
            {
                PlaceFormationMember(prefab, cc, rr, row, mapRow);
            }
            return true;
        }

        private FormationSpawn PickFormation(int row, float t)
        {
            float total = 0f;
            foreach (FormationSpawn f in _formationTable)
            {
                if (f != null && f.enemy != null && row >= f.minRow)
                {
                    total += Mathf.Max(0f, f.weight);
                }
            }
            if (total <= 0f)
            {
                return null;
            }

            float pick = Mathf.Clamp01(t) * total;
            float acc = 0f;
            foreach (FormationSpawn f in _formationTable)
            {
                if (f == null || f.enemy == null || row < f.minRow)
                {
                    continue;
                }
                acc += Mathf.Max(0f, f.weight);
                if (pick <= acc)
                {
                    return f;
                }
            }
            return null;
        }

        /// <summary>앵커(col,row) 를 좌상단 기준으로 편대 칸 목록을 만든다. 맵 밖으로 나가면 false.</summary>
        private bool BuildFormationCells(FormationSpawn f, int col, int row, List<(int, int, GridEntity)> outCells)
        {
            switch (f.shape)
            {
                case FormationShape.Line:
                {
                    int n = Mathf.Max(1, f.span);
                    if (col + n > Columns)
                    {
                        return false;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        outCells.Add((col + i, row, f.enemy));
                    }
                    return true;
                }

                case FormationShape.Pyramid:
                {
                    int n = Mathf.Max(1, f.span);
                    if (n % 2 == 0) n--;          // 홀수 밑변
                    if (col + n > Columns)
                    {
                        return false;
                    }
                    int layers = (n + 1) / 2;
                    for (int L = 0; L < layers; L++)
                    {
                        int w = 2 * L + 1;
                        int start = col + (n - w) / 2;
                        bool baseRow = L == layers - 1;
                        for (int i = 0; i < w; i++)
                        {
                            // 속은 비움: 각 층의 양 끝(빗변)과 맨 아래 줄(밑변)만
                            if (baseRow || i == 0 || i == w - 1)
                            {
                                outCells.Add((start + i, row + L, f.enemy));
                            }
                        }
                    }
                    return true;
                }

                case FormationShape.ChestGuard:
                {
                    if (col + 3 > Columns)
                    {
                        return false;
                    }
                    GridEntity chestPrefab = f.chest != null ? f.chest.GetComponent<GridEntity>() : null;
                    for (int dy = 0; dy < 3; dy++)
                    {
                        for (int dx = 0; dx < 3; dx++)
                        {
                            bool center = dx == 1 && dy == 1;
                            GridEntity prefab = center ? chestPrefab : f.enemy;
                            if (prefab == null)
                            {
                                continue; // 상자 미지정이면 가운데는 비움
                            }
                            outCells.Add((col + dx, row + dy, prefab));
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private void PlaceFormationMember(GridEntity prefab, int cc, int rr, int buildRow, MapRow buildMapRow)
        {
            GridEntity e = GetPool(prefab).Get();
            _entityPrefabOf[e] = prefab;
            e.Place(this, cc, rr, CellToWorld(cc, rr));

            if (rr == buildRow)
            {
                buildMapRow.Entities[cc] = e;
            }
            else if (_rows.TryGetValue(rr, out MapRow other))
            {
                other.Entities[cc] = e;
            }
            else
            {
                _pendingEntityCells[CellKey(cc, rr)] = e;
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

        /// <summary>
        /// 이 칸의 지층이 인접 시 플레이어에게 주는 접촉 피해. 빈 칸/엔티티 칸/범위 밖은 0.
        /// (지층 자체가 "기본 적". 격자 엔티티(<see cref="GridEntity"/>)는 자체적으로 피해를 처리한다.)
        /// </summary>
        public int ContactDamageAt(int col, int row)
        {
            if (col < 0 || col >= Columns || row < 0)
            {
                return 0;
            }
            if (!_rows.TryGetValue(row, out MapRow r))
            {
                return 0;
            }
            if (r.Entities[col] != null)
            {
                return 0;
            }

            BlockData b = r.Cells[col];
            if (!b.IsSolid)
            {
                return 0;
            }

            switch (b.Type)
            {
                case StrataType.Rock: return _rockContactDamage;
                case StrataType.Ore: return _oreContactDamage;
                case StrataType.Soil: return _soilContactDamage;
                default: return 0;
            }
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
        public bool DamageCell(int col, int row, int amount, bool isCrit = false)
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
                DamageDealt?.Invoke(entity.transform.position, amount, isCrit, true); // 엔티티 중심
                return entity.ApplyDamage(amount); // 사망 시 내부에서 ClearEntity 호출
            }

            // 지층 블록
            BlockData data = mapRow.Cells[col];
            if (!data.IsSolid)
            {
                return false;
            }

            DamageDealt?.Invoke(CellToWorld(col, row), amount, isCrit, false);

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
