using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Map;
using Game.UI;

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
        [Tooltip("HUD 가상 조이스틱. 있으면 눌린 동안 키보드 대신 이걸 읽음.")]
        [SerializeField] private VirtualJoystick _joystick;

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

        [Header("Debug")]
#if UNITY_EDITOR
        [Tooltip("좌상단에 상태 표시 (col/row/hp/플래그). 에디터 전용.")]
        [SerializeField] private bool _showDebugHud = false;
#endif
        [Tooltip("무적 치트. 메뉴 '1655M/Debug: Player Invincible' 로도 토글 (재시작해도 유지).")]
        [SerializeField] private bool _godMode = false;

        internal const string GodModeKey = "debug_godmode";
        public bool GodMode => _godMode;

        // 이벤트 ----------------------------------------------------------
        public event Action<int, int> CellChanged;          // (col, row) 새 칸에 도착
        public event Action<int, int, BlockData> Dug;       // (col, row, 파괴 전 데이터) 채굴 시도
        public event Action<int, bool> HitDealt;            // (피해량, 치명타 여부) 타격 시
        public event Action<int, int> Attacked;            // (dCol, dRow) 근접 타격 방향 - 공격 애니메이션용
        public event Action<int> HpChanged;                 // 현재 HP
        public event Action<int> MaxHpChanged;              // 최대 HP (런 시작 시)
        public event Action<int, Vector3> DamageTaken;      // (받은 피해량, 피해원 월드 위치)
        public event Action<bool> ShieldChanged;            // 보호막 on/off
        public event Action Died;
        public event Action DashStarted;
        public event Action DashEnded;
        public event Action<int, int> DashAffectedCell;     // (col, row) 돌진이 타격한 칸 - 적 피해 훅
        public event Action<int, int, int> ShockwaveFired;  // (중심 col, row, 반경) - 확산 연출용

        /// <summary>씬 단위 싱글톤. 씬 리로드마다 새로 생성.</summary>
        public static PlayerController Instance { get; private set; }

        // 상태 ----------------------------------------------------------
        public int Column => _col;
        public int Row => _row;
        public int Depth => _row;                           // 심도 = row
        public int Hp => _hp;
        public int MaxHp => _maxHp;
        public bool IsAlive => _hp > 0;
        public bool IsDashing => _dashing;
        public bool IsBusy => _isAnimating || _dashing;
        public bool ShieldActive => _shieldTimer > 0f;
        public float ShieldRemaining => Mathf.Max(0f, _shieldTimer);

        private int _col;
        private int _row;
        private int _hp;
        private int _maxHp;
        private int _damage;
        private float _critChance;
        private float _critMultiplier;
        private bool _dashing;
        private bool _controlEnabled = true;
        private bool _levitating;   // 토네이도 등에 붕 떠 있음 — 조작 불가
        private float _shieldTimer;
        private float _shieldReduction;

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
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerController] 중복 인스턴스 - 파괴", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (PlayerPrefs.GetInt(GodModeKey, 0) == 1)
            {
                _godMode = true;
            }

            ApplyStats();

            if (_inputActions != null)
            {
                _moveAction = _inputActions.FindActionMap("Player", throwIfNotFound: true)
                                           .FindAction("Move", throwIfNotFound: true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
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
            MaxHpChanged?.Invoke(_maxHp);
            HpChanged?.Invoke(_hp);
        }

        /// <summary>PlayerStats(기본값 + Status 업그레이드 + 장착 검)에서 스탯을 읽어 캐시.</summary>
        private void ApplyStats()
        {
            _maxHp = PlayerStats.MaxHp;
            _hp = _maxHp;
            _damage = PlayerStats.Damage;
            _critChance = PlayerStats.CritChance;
            _critMultiplier = PlayerStats.CritMultiplier;
        }

        // ------------------------------------------------------------------
        // 보호막 (스킬)
        // ------------------------------------------------------------------

        /// <summary><paramref name="duration"/> 초 동안 받는 피해를 <paramref name="reduction"/>(0~1) 만큼 감소.</summary>
        public void ActivateShield(float duration, float reduction)
        {
            _shieldTimer = Mathf.Max(_shieldTimer, duration);
            _shieldReduction = Mathf.Clamp01(reduction);
            ShieldChanged?.Invoke(true);
        }

        private void TickShield()
        {
            if (_shieldTimer <= 0f)
            {
                return;
            }
            _shieldTimer -= Time.deltaTime;
            if (_shieldTimer <= 0f)
            {
                _shieldTimer = 0f;
                _shieldReduction = 0f;
                ShieldChanged?.Invoke(false);
            }
        }

        /// <summary>이번 타격 피해. 치명타 확률에 따라 배수 적용. HitDealt 이벤트도 발생.</summary>
        private int RollHit(out bool crit)
        {
            crit = UnityEngine.Random.value * 100f < _critChance;
            int dmg = crit ? Mathf.RoundToInt(_damage * _critMultiplier) : _damage;
            HitDealt?.Invoke(dmg, crit);
            return dmg;
        }

        private void Update()
        {
            if (_map == null)
            {
                return;
            }

            TickShield(); // 버프는 상태 무관 실시간

            if (_levitating)
            {
                return; // 토네이도 코루틴이 위치를 제어
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

            if (!_controlEnabled)
            {
                return; // 시작 UI 등에서 조작 비활성
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
            CheckContactDamage();
            return true;
        }

        // ------------------------------------------------------------------
        // 접촉 피해 (지층 = 기본 적. 좌·우·아래 인접 지층에서 이동 시마다 1회)
        // ------------------------------------------------------------------

        private void CheckContactDamage()
        {
            if (_map == null || !IsAlive)
            {
                return;
            }

            ApplyContactFrom(_col - 1, _row);       // 좌
            if (IsAlive) ApplyContactFrom(_col + 1, _row);  // 우
            if (IsAlive) ApplyContactFrom(_col, _row + 1);  // 아래
        }

        private void ApplyContactFrom(int col, int row)
        {
            int dmg = _map.ContactDamageAt(col, row);
            if (dmg > 0)
            {
                Damage(dmg, _map.CellToWorld(col, row));
                // TODO: 방향별 피격 연출 / 넉백 / 사운드
            }
        }

        // ------------------------------------------------------------------
        // 입력 → 이동 / 채굴
        // ------------------------------------------------------------------

        private void HandleMoveInput()
        {
            if (_moveAction == null && _joystick == null)
            {
                return;
            }

            Vector2 raw = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (_joystick != null && _joystick.IsActive)
            {
                raw = _joystick.Value; // 조이스틱 누르는 동안 우선
            }

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
                int hit = RollHit(out bool crit);
                _map.DamageCell(tc, tr, hit, crit);
                if (before.IsSolid) // 지층 블록이었을 때만 채굴 이벤트 (적 타격 연출은 별도)
                {
                    Dug?.Invoke(tc, tr, before);
                }
                Attacked?.Invoke(dc, dr);
                _actionTimer = _digInterval;
                return;
            }

            // 이동
            _col = tc;
            _row = tr;
            BeginAnimation(_map.CellToWorld(_col, _row), _stepDuration);
            _actionTimer = _stepInterval;
            CellChanged?.Invoke(_col, _row);
            CheckContactDamage();
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
            return IsAlive && _controlEnabled && _map != null && !_dashing && !_isAnimating;
        }

        /// <summary>
        /// 현재 위치에서 수직 아래로 <paramref name="distance"/> 칸까지 빠르게 돌진한다.
        /// 진행 방향(아래) + 좌우 <paramref name="widthRadius"/> 칸을 <paramref name="damageMultiplier"/> 배 피해로 타격.
        /// 정면을 못 뚫으면 그 지점에서 멈춘다.
        /// </summary>
        public bool StartDash(int distance, float damageMultiplier, int widthRadius, float stepDuration)
        {
            if (!CanDash())
            {
                return false;
            }

            StartCoroutine(DashRoutine(
                Mathf.Max(1, distance),
                Mathf.Max(0.1f, damageMultiplier),
                Mathf.Max(0, widthRadius),
                Mathf.Max(0.01f, stepDuration)));
            return true;
        }

        private IEnumerator DashRoutine(int distance, float damageMultiplier, int widthRadius, float stepDuration)
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
                            int hit = Mathf.RoundToInt(RollHit(out bool crit) * damageMultiplier);
                            _map.DamageCell(c, tr, hit, crit);
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
                    CheckContactDamage();

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

        /// <summary>
        /// 단위 방향열(<paramref name="steps"/>)을 따라 연속 돌진. (아이템: ㄹ자 대시)
        /// 각 칸의 지층·엔티티를 <paramref name="damageMultiplier"/> 배로 타격하고, 못 뚫으면 그 지점에서 정지.
        /// dir 은 (±1,0)·(0,+1) 단위로 해석. 위(-y)와 범위 밖 열은 건너뛴다.
        /// </summary>
        public bool StartPathDash(Vector2Int[] steps, float damageMultiplier, float stepDuration)
        {
            if (steps == null || steps.Length == 0 || !CanDash())
            {
                return false;
            }
            StartCoroutine(PathDashRoutine(steps, Mathf.Max(0.1f, damageMultiplier), Mathf.Max(0.01f, stepDuration)));
            return true;
        }

        private IEnumerator PathDashRoutine(Vector2Int[] steps, float damageMultiplier, float stepDuration)
        {
            _dashing = true;
            DashStarted?.Invoke();
            try
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    if (!IsAlive)
                    {
                        yield break;
                    }

                    Vector2Int d = steps[i];
                    int tc = _col + (d.x > 0 ? 1 : (d.x < 0 ? -1 : 0));
                    int tr = _row + (d.y > 0 ? 1 : 0);   // 위로는 못 감

                    if (tc < 0 || tc >= MapGenerator.Columns || (tc == _col && tr == _row))
                    {
                        continue;
                    }

                    if (_map.IsSolid(tc, tr))
                    {
                        int hit = Mathf.RoundToInt(RollHit(out bool crit) * damageMultiplier);
                        _map.DamageCell(tc, tr, hit, crit);
                    }
                    DashAffectedCell?.Invoke(tc, tr);

                    if (!_map.IsRowReady(tr) || _map.IsSolid(tc, tr))
                    {
                        yield break; // 못 뚫음 → 정지
                    }

                    _col = tc;
                    _row = tr;
                    BeginAnimation(_map.CellToWorld(_col, _row), stepDuration);
                    CellChanged?.Invoke(_col, _row);
                    CheckContactDamage();

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
        // 충격파 (스킬)
        // ------------------------------------------------------------------

        /// <summary>플레이어 중심 반경 <paramref name="radius"/> 원형 범위의 지층·엔티티에 즉발 피해.</summary>
        public void Shockwave(int radius, float damageMultiplier)
        {
            if (_map == null || radius <= 0)
            {
                return;
            }

            int r2 = radius * radius;
            for (int dr = -radius; dr <= radius; dr++)
            {
                for (int dc = -radius; dc <= radius; dc++)
                {
                    if (dc == 0 && dr == 0)
                    {
                        continue;
                    }
                    if (dc * dc + dr * dr > r2)
                    {
                        continue; // 원형
                    }

                    int c = _col + dc;
                    int rw = _row + dr;
                    if (c < 0 || c >= MapGenerator.Columns || rw < 0 || !_map.IsRowReady(rw))
                    {
                        continue;
                    }
                    if (_map.IsSolid(c, rw))
                    {
                        int hit = Mathf.RoundToInt(RollHit(out bool crit) * damageMultiplier);
                        _map.DamageCell(c, rw, hit, crit);
                    }
                }
            }

            ShockwaveFired?.Invoke(_col, _row, radius); // 히트스톱/흔들림은 JuiceDirector 가 처리
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

        /// <param name="source">피해원 월드 위치 (데미지 텍스트가 여기서 생성됨).</param>
        public void Damage(int amount, Vector3 source)
        {
            if (amount <= 0 || !IsAlive || _godMode)
            {
                return;
            }

            if (_shieldTimer > 0f)
            {
                amount = Mathf.Max(0, Mathf.RoundToInt(amount * (1f - _shieldReduction)));
                if (amount <= 0)
                {
                    return; // 완전 차단
                }
            }

            _hp = Mathf.Max(0, _hp - amount);
            DamageTaken?.Invoke(amount, source);
            HpChanged?.Invoke(_hp);
            // TODO: 무적시간 / 피격 넉백 / 히트 연출

            if (_hp == 0)
            {
                Died?.Invoke();
                // TODO: 사망 연출 → 결과 화면
            }
        }

        /// <summary>시작 UI / 컷신 등에서 플레이어 조작(이동·낙하·스킬 이동)을 켜고 끈다.</summary>
        public void SetControlEnabled(bool value)
        {
            _controlEnabled = value;
        }

        public bool IsLevitating => _levitating;

        /// <summary>
        /// 토네이도 등에 붕 떠올랐다가 내려온다. <paramref name="duration"/> 동안 조작 불가.
        /// 격자 좌표(col/row)는 그대로 — 뜬 자리로 다시 내려온다. 피해가 있으면 시작 시 1회 적용.
        /// </summary>
        public void Levitate(float peakHeight, float duration, int damage, Vector3 source)
        {
            if (!IsAlive || _levitating || _dashing)
            {
                return; // 돌진 중엔 상태 충돌 → 이번 토네이도는 흘림
            }
            if (damage > 0)
            {
                Damage(damage, source);
            }
            if (!IsAlive)
            {
                return;
            }
            StartCoroutine(LevitateRoutine(Mathf.Max(0.1f, peakHeight), Mathf.Max(0.1f, duration)));
        }

        private IEnumerator LevitateRoutine(float peak, float dur)
        {
            _levitating = true;
            _isAnimating = false;
            _dashing = false;

            Vector3 ground = _map != null ? _map.CellToWorld(_col, _row) : transform.position;
            float t = 0f;
            while (t < 1f && IsAlive)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);
                transform.position = ground + Vector3.up * (Mathf.Sin(k * Mathf.PI) * peak);
                yield return null;
            }

            transform.position = ground;
            _levitating = false;
            _actionTimer = 0.1f;
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

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_showDebugHud)
            {
                return;
            }

            string s = $"cell ({_col},{_row})  depth {_row}\n" +
                       $"HP {_hp}/{_maxHp}  alive={IsAlive}" + (_godMode ? "  [GOD]" : "") + "\n" +
                       $"anim={_isAnimating} dash={_dashing} timer={_actionTimer:F2}\n" +
                       $"rowReady(below)={( _map != null && _map.IsRowReady(_row + 1))}";
            GUI.Label(new Rect(10, 10, 400, 90), s);
        }

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
