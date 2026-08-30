using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Map
{
    /// <summary>
    /// 가로 9칸 고정, 세로 무한 그리드 지층 맵.
    ///
    /// 좌표계: 셀 (col, row).
    ///   - col : 0 ~ 8, 왼쪽 → 오른쪽. col 4 가 월드 x=0 (중앙).
    ///   - row : 0(지표) 부터 아래로 증가. 깊을수록 row 값이 크다. 월드 y 는 감소.
    ///
    /// 추적 대상(_tracked: 보통 플레이어)의 깊이를 기준으로,
    /// 아래쪽 행은 미리 생성하고 화면 위로 벗어난 행은 풀에 반납한다.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        public const int Columns = 9;

        [Header("References")]
        [SerializeField] private BlockView _blockPrefab;
        [Tooltip("이 대상의 y 위치를 기준으로 행을 스트리밍한다 (보통 플레이어).")]
        [SerializeField] private Transform _tracked;
        [Tooltip("생성된 블록들의 부모이자 그리드 (col 0, row 0) 의 원점. 비우면 이 오브젝트가 기준.")]
        [SerializeField] private Transform _blockRoot;

        /// <summary>그리드 원점 + 블록 부모. 인스펙터 미할당이어도(에디트 모드 포함) 안전하게 자기 Transform 사용.</summary>
        private Transform Root => _blockRoot != null ? _blockRoot : transform;

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

        /// <summary>블록이 완전히 파괴됐을 때: (col, row, 파괴된 블록 데이터).</summary>
        public event Action<int, int, BlockData> BlockDestroyed;

        // 활성 행: row -> 행 데이터.
        private readonly Dictionary<int, MapRow> _rows = new();
        private ObjectPool<BlockView> _pool;

        private int _topRow;    // 현재 유지 중인 가장 얕은 행
        private int _bottomRow; // 현재 유지 중인 가장 깊은 행
        private bool _initialized;

        // ------------------------------------------------------------------
        // 라이프사이클
        // ------------------------------------------------------------------

        private void Awake()
        {
            int prewarm = Columns * (_rowsAhead + _rowsBehind + 4);
            _pool = new ObjectPool<BlockView>(
                createFunc: CreateView,
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

        private BlockView CreateView()
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

            // 아래로 확장
            for (int row = _bottomRow + 1; row <= wantBottom; row++)
            {
                BuildRow(row);
            }
            if (wantBottom > _bottomRow)
            {
                _bottomRow = wantBottom;
            }

            // 위쪽 회수
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
            for (int col = 0; col < Columns; col++)
            {
                BlockData data = GenerateBlock(col, row);
                mapRow.Cells[col] = data;

                if (data.IsSolid)
                {
                    BlockView view = _pool.Get();
                    view.transform.position = CellToWorld(col, row);
                    view.Bind(col, row, data);
                    mapRow.Views[col] = view;
                }
            }

            _rows.Add(row, mapRow);
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
                    _pool.Release(mapRow.Views[col]);
                    mapRow.Views[col] = null;
                }
            }

            _rows.Remove(row);
        }

        // ------------------------------------------------------------------
        // 지층 생성 규칙 (임시)
        // ------------------------------------------------------------------

        private BlockData GenerateBlock(int col, int row)
        {
            // 지표는 비우고, 그 아래는 항상 solid.
            // 빈 공간은 오직 플레이어가 파낸 결과로만 생긴다.
            if (row < _surfaceRows)
            {
                return BlockData.Empty;
            }

            StrataType type = LayerAt(row);
            return new BlockData(type, HpFor(type));
        }

        /// <summary>깊이(row)에 해당하는 지층 종류. 층을 위에서부터 두께만큼 쌓고,
        /// 마지막 층보다 깊으면 마지막 층 종류를 계속 사용한다.</summary>
        private StrataType LayerAt(int row)
        {
            if (_layers == null || _layers.Length == 0)
            {
                return StrataType.Soil;
            }

            int depth = row - _surfaceRows; // 첫 층 기준 0부터
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

            return _layers[_layers.Length - 1].type; // 마지막 층 이후로 계속 이어짐
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
            return _rows.TryGetValue(row, out MapRow r) && r.Cells[col].IsSolid;
        }

        /// <summary>해당 행이 이미 생성(스트리밍)되어 데이터가 유효한지. 낙하 판정 등에서 사용.</summary>
        public bool IsRowReady(int row)
        {
            return _rows.ContainsKey(row);
        }

        public BlockData GetBlock(int col, int row)
        {
            return _rows.TryGetValue(row, out MapRow r) ? r.Cells[col] : BlockData.Empty;
        }

        /// <summary>
        /// 특정 칸에 피해를 준다. HP 가 0 이하가 되면 파괴하고 <see cref="BlockDestroyed"/> 를 발생시킨다.
        /// </summary>
        /// <returns>이번 호출로 블록이 파괴됐으면 true.</returns>
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

            // 파괴
            BlockData destroyed = data;
            mapRow.Cells[col] = BlockData.Empty;
            if (mapRow.Views[col] != null)
            {
                _pool.Release(mapRow.Views[col]);
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

        private class MapRow
        {
            public readonly BlockData[] Cells = new BlockData[Columns];
            public readonly BlockView[] Views = new BlockView[Columns];
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
