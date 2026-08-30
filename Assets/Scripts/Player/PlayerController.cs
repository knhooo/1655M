using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Map;

namespace Game.Player
{
    /// <summary>
    /// 그리드 스텝 이동 플레이어.
    ///  - 이동: 좌 / 우 / 아래 / 좌하 / 우하 (위쪽 없음).
    ///  - 막힌 칸으로 이동을 시도하면 이동 대신 그 칸을 "자동 채굴".
    ///  - 발밑이 비면 낙하.
    /// 상태 머신 없이 매 틱(애니메이션 중이 아닐 때) 낙하 → 입력 순으로 처리.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapGenerator _map;
        [Tooltip("InputSystem_Actions 에셋 (Player 맵의 Move 사용).")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Spawn")]
        [SerializeField] private int _startColumn = 4;
        [Tooltip("지표(row 0)는 비어 있으므로 0에서 시작하면 흙 위에 선다.")]
        [SerializeField] private int _startRow = 0;

        [Header("Movement")]
        [Tooltip("한 칸 이동에 걸리는 시간(초).")]
        [SerializeField] private float _stepDuration = 0.10f;
        [Tooltip("한 칸 낙하에 걸리는 시간(초). 이동보다 빠르게.")]
        [SerializeField] private float _fallDuration = 0.06f;
        [Tooltip("채굴 시도 후 다음 입력까지의 간격(초).")]
        [SerializeField] private float _digInterval = 0.12f;
        [Tooltip("입력이 없을 때도 유지되는 스텝 간 최소 간격(초).")]
        [SerializeField] private float _stepInterval = 0.04f;

        [Header("Dig (이동 = 채굴 = 적 타격, 모두 동일)")]
        [Tooltip("막힌 칸으로 이동 시도 시 그 칸(지층/적)에 주는 피해.")]
        [SerializeField] private int _digPower = 1;

        [Header("Stats (임시)")]
        [SerializeField] private int _maxHp = 5;

        [Header("Debug")]
        [Tooltip("좌상단에 상태 표시 (col/row/hp/플래그).")]
        [SerializeField] private bool _showDebugHud = true;

        // 이벤트 ----------------------------------------------------------
        public event Action<int, int> CellChanged;          // (col, row) 새 칸에 도착
        public event Action<int, int, BlockData> Dug;       // (col, row, 파괴 전 데이터) 채굴 시도
        public event Action<int> HpChanged;                 // 현재 HP
        public event Action Died;
        public event Action DashStarted;
        public event Action DashEnded;
        public event Action<int, int> DashAffectedCell;     // (col, row) 돌진이 타격한 칸 - 적 피해 훅

        // 상태 ----------------------------------------------------------
        public int Column => _col;
        public int Row => _row;
        public int Depth => _row;                           // 심도 = row
        public int Hp => _hp;
        public bool IsAlive => _hp > 0;
        public bool IsDashing => _dashing;
        public bool IsBusy => _isAnimating || _dashing;

        private int _col;
        private int _row;
        private int _hp;
        private bool _dashing;

        private InputAction _moveAction;

        private bool _isAnimating;
        private Vector3 _animFrom;
        private Vector3 _animTo;
        private float _animElapsed;
        private float _animDuration;
        private float _actionTimer;   // 다음 행동까지 남은 시간

        // ------------------------------------------------------------------

        private void Awake()
        {
            _hp = _maxHp;

            if (_inputActions != null)
            {
                _moveAction = _inputActions.FindActionMap("Player", throwIfNotFound: true)
                                           .FindAction("Move", throwIfNotFound: true);
            }
        }

        private void OnEnable() => _moveAction?.Enable();
        private void OnDisable() => _moveAction?.Disable();

        private void Start()
        {
            _col = Mathf.Clamp(_startColumn, 0, MapGenerator.Columns - 1);
            _row = Mathf.Max(0, _startRow);

            if (_map != null)
            {
                transform.position = _map.CellToWorld(_col, _row);
            }
            CellChanged?.Invoke(_col, _row);
            HpChanged?.Invoke(_hp);
        }

        private void Update()
        {
            if (_map == null)
            {
                return;
            }

            // 진행 중인 칸 이동은 죽더라도 끝까지 재생한다 (여기서 멈추면 코루틴/플래그가 stuck 된다).
            if (_isAnimating)
            {
                TickAnimation();
                return;
            }

            if (!IsAlive)
            {
                return; // 사망 시 정지 (애니메이션은 위에서 이미 마무리됨)
            }

            if (_dashing)
            {
                return; // 돌진 코루틴이 이동을 제어
            }

            if (_actionTimer > 0f)
            {
                _actionTimer -= Time.deltaTime;
                return;
            }

            // 1) 낙하 우선
            if (TryFall())
            {
                return;
            }

            // 2) 입력 처리 (이동 / 자동 채굴 / 적 타격)
            HandleMoveInput();
        }

        // ------------------------------------------------------------------
        // 낙하
        // ------------------------------------------------------------------

        private bool TryFall()
        {
            int below = _row + 1;
            if (!_map.IsRowReady(below))
            {
                return false; // 아직 생성 안 된 영역 위에서는 낙하하지 않음
            }
            if (_map.IsSolid(_col, below))
            {
                return false; // 지지됨
            }

            _row = below;
            BeginAnimation(_map.CellToWorld(_col, _row), _fallDuration);
            CellChanged?.Invoke(_col, _row);
            return true;
        }

        // ------------------------------------------------------------------
        // 입력 → 이동 / 채굴
        // ------------------------------------------------------------------

        private void HandleMoveInput()
        {
            if (_moveAction == null)
            {
                return;
            }

            Vector2 raw = _moveAction.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.25f)
            {
                return;
            }

            if (!TryResolveDirection(raw, out int dc, out int dr))
            {
                return;
            }

            int tc = _col + dc;
            int tr = _row + dr;

            if (tc < 0 || tc >= MapGenerator.Columns || tr < 0)
            {
                return;
            }

            if (!_map.IsRowReady(tr))
            {
                return; // 아직 생성되지 않은 영역으로는 이동/채굴 불가 (빈 공간으로 새는 것 방지)
            }

            if (_map.IsSolid(tc, tr))
            {
                // 이동하지 않고 그 칸을 타격 — 지층이든 적이든 동일하게 HP 를 깎는다.
                BlockData before = _map.GetBlock(tc, tr);
                _map.DamageCell(tc, tr, _digPower);
                if (before.IsSolid) // 지층 블록이었을 때만 채굴 이벤트 (적 타격 연출은 별도)
                {
                    Dug?.Invoke(tc, tr, before);
                }
                _actionTimer = _digInterval;
                return;
            }

            // 이동
            _col = tc;
            _row = tr;
            BeginAnimation(_map.CellToWorld(_col, _row), _stepDuration);
            _actionTimer = _stepInterval;
            CellChanged?.Invoke(_col, _row);
        }

        /// <summary>입력 벡터를 5방향 중 하나로 양자화. 위쪽/무효면 false.</summary>
        private static bool TryResolveDirection(Vector2 raw, out int dc, out int dr)
        {
            int x = Mathf.Abs(raw.x) > 0.5f ? (int)Mathf.Sign(raw.x) : 0;
            int y = raw.y < -0.5f ? -1 : (raw.y > 0.5f ? 1 : 0);

            // 그리드: 아래로 갈수록 row 증가 → 입력 y가 음수일 때 dr = +1
            dr = y < 0 ? 1 : 0;
            dc = x;

            if (dr == 0 && dc == 0)
            {
                return false; // 위 또는 입력 없음
            }
            if (dr == 0 && y > 0)
            {
                return false; // 순수 위쪽 이동 금지
            }
            return true;
        }

        // ------------------------------------------------------------------
        // 돌진 (스킬에서 호출)
        // ------------------------------------------------------------------

        public bool CanDash()
        {
            return IsAlive && _map != null && !_dashing && !_isAnimating;
        }

        /// <summary>
        /// 현재 위치에서 수직 아래로 <paramref name="distance"/> 칸까지 빠르게 돌진한다.
        /// 진행 방향(아래) + 좌우 <paramref name="widthRadius"/> 칸을 <paramref name="digPower"/> 로 타격.
        /// 정면을 못 뚫으면 그 지점에서 멈춘다.
        /// </summary>
        public bool StartDash(int distance, int digPower, int widthRadius, float stepDuration)
        {
            if (!CanDash())
            {
                return false;
            }

            StartCoroutine(DashRoutine(
                Mathf.Max(1, distance),
                Mathf.Max(1, digPower),
                Mathf.Max(0, widthRadius),
                Mathf.Max(0.01f, stepDuration)));
            return true;
        }

        private IEnumerator DashRoutine(int distance, int digPower, int widthRadius, float stepDuration)
        {
            _dashing = true;
            DashStarted?.Invoke();

            try
            {
                for (int step = 0; step < distance; step++)
                {
                    if (!IsAlive)
                    {
                        yield break;
                    }

                    int tr = _row + 1;

                    // 넓은 타격: 아래 칸 + 좌우 widthRadius
                    for (int dc = -widthRadius; dc <= widthRadius; dc++)
                    {
                        int c = _col + dc;
                        if (c < 0 || c >= MapGenerator.Columns)
                        {
                            continue;
                        }
                        if (_map.IsSolid(c, tr))
                        {
                            _map.DamageCell(c, tr, digPower);
                        }
                        DashAffectedCell?.Invoke(c, tr);
                    }

                    // 정면(아래)을 못 뚫었으면 정지
                    if (!_map.IsRowReady(tr) || _map.IsSolid(_col, tr))
                    {
                        yield break;
                    }

                    _row = tr;
                    BeginAnimation(_map.CellToWorld(_col, _row), stepDuration);
                    CellChanged?.Invoke(_col, _row);

                    while (_isAnimating)
                    {
                        yield return null;
                    }
                }
            }
            finally
            {
                _dashing = false;
                _actionTimer = _digInterval;
                DashEnded?.Invoke();
            }
        }

        // ------------------------------------------------------------------
        // 애니메이션 (칸 → 칸 보간)
        // ------------------------------------------------------------------

        private void BeginAnimation(Vector3 to, float duration)
        {
            _animFrom = transform.position;
            _animTo = to;
            _animElapsed = 0f;
            _animDuration = Mathf.Max(0.01f, duration);
            _isAnimating = true;
        }

        private void TickAnimation()
        {
            _animElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_animElapsed / _animDuration);
            transform.position = Vector3.LerpUnclamped(_animFrom, _animTo, t);

            if (t >= 1f)
            {
                transform.position = _animTo;
                _isAnimating = false;
            }
        }

        // ------------------------------------------------------------------
        // 체력 (뼈대)
        // ------------------------------------------------------------------

        public void Damage(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return;
            }

            _hp = Mathf.Max(0, _hp - amount);
            HpChanged?.Invoke(_hp);
            // TODO: 무적시간 / 피격 넉백 / 히트 연출

            if (_hp == 0)
            {
                Debug.Log($"[Player] 사망 - depth {_row}", this);
                Died?.Invoke();
                // TODO: 사망 연출 → 결과 화면
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return;
            }
            _hp = Mathf.Min(_maxHp, _hp + amount);
            HpChanged?.Invoke(_hp);
        }

        private void OnGUI()
        {
            if (!_showDebugHud)
            {
                return;
            }

            string s = $"cell ({_col},{_row})  depth {_row}\n" +
                       $"HP {_hp}/{_maxHp}  alive={IsAlive}\n" +
                       $"anim={_isAnimating} dash={_dashing} timer={_actionTimer:F2}\n" +
                       $"rowReady(below)={( _map != null && _map.IsRowReady(_row + 1))}";
            GUI.Label(new Rect(10, 10, 400, 90), s);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_map == null)
            {
                return;
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_map.CellToWorld(_col, _row), Vector3.one * 0.9f);
        }
#endif
    }
}
