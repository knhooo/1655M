using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;
using Game.Audio;

namespace Game.Enemies
{
    /// <summary>
    /// 보스. 격자에 <b>9x9</b> 로 박혀 제자리에서 공격. 파괴될 때까지 진로를 완전히 막는다(가로 전폭).
    ///  - 붙어 있으면 <see cref="_contactInterval"/> 초마다 접촉 피해 (등장 즉시)
    ///  - <b>플레이어가 처음 때린 뒤부터</b> <see cref="_attackInterval"/> 마다 패턴 1회: 텔레그래프(경고) → 딜레이 → 강타
    ///     · ColumnStrike : 플레이어가 선 세로 직선
    ///     · RowStrike    : 플레이어가 선 가로 직선
    ///  - 패턴은 <see cref="ChooseAttack"/> / <see cref="BuildAttackCells"/> / <see cref="OnAttackStrike"/>
    ///    오버라이드로 서브클래스(Boss2)가 확장한다.
    ///
    /// 프리팹: GridEntity 의 Size 를 (9,9) 로. 스폰은 <see cref="MapGenerator"/> 의 _bosses.
    /// </summary>
    public class Boss : GridEntity
    {
        // 공격 ID. 서브클래스(Boss2 등)는 2 이상을 추가해 확장한다.
        protected const int AttackColumnStrike = 0;
        protected const int AttackRowStrike = 1;

        [Header("Boss")]
        [SerializeField] private int _maxHp = 3000;
        [Tooltip("붙어 있을 때 1회 접촉 피해.")]
        [SerializeField] private int _contactDamage = 40;
        [Tooltip("접촉 피해 재적용 간격(초).")]
        [SerializeField] private float _contactInterval = 1f;

        [Header("공격")]
        [Tooltip("공격 시도 간격(초).")]
        [SerializeField] private float _attackInterval = 3f;
        [Tooltip("플레이어가 처음 때린 뒤 첫 공격까지 대기(초).")]
        [SerializeField] private float _attackWindup = 2.5f;
        [Tooltip("경고 표시 후 강타까지 딜레이(초). 피할 시간.")]
        [SerializeField] private float _telegraphTime = 0.9f;
        [Tooltip("강타에 맞았을 때 플레이어 피해.")]
        [SerializeField] private int _attackDamage = 60;
        [Tooltip("ColumnStrike 세로 길이(칸). 보스 머리 위에서부터 위로.")]
        [SerializeField, Min(1)] private int _columnLength = 9;

        [Header("사망 보상")]
        [SerializeField, Min(0)] private int _coinReward = 150;

        /// <summary>HP 변화: (현재, 최대). 보스 HP 바용. 배치 시 최대치로도 발생.</summary>
        public event Action<int, int> HealthChanged;
        /// <summary>처치: (앵커 col, 앵커 row, 지급 coin).</summary>
        public event Action<int, int, int> Killed;
        /// <summary>공격 경고: (대상 칸들, 딜레이). 경고 스프라이트 표시용.</summary>
        public event Action<IReadOnlyList<Vector2Int>, float> AttackTelegraph;
        /// <summary>강타 발동: (대상 칸들). 임팩트 연출용.</summary>
        public event Action<IReadOnlyList<Vector2Int>> AttackStrike;
        /// <summary>페이즈 전환: 새 페이즈 번호(현재 2 로만). 연출/사운드 훅.</summary>
        public event Action<int> PhaseChanged;

        public int MaxHp => Mathf.Max(1, _maxHp);
        public int Hp => _hp;
        public bool IsAlive => _hp > 0;

        /// <summary>HP 절반 이하로 떨어진 뒤 (한 번 넘어가면 유지).</summary>
        protected int Phase { get; private set; } = 1;

        private int _hp;
        private float _contactCd;
        private float _attackCd;
        private bool _attacking;
        private bool _engaged;   // 플레이어가 처음 때린 뒤부터 공격 시작
        protected int _attackIndex;

        private readonly List<Vector2Int> _cells = new();
        private WaitForSeconds _telegraphWait;

        protected override void OnPlaced()
        {
            _hp = MaxHp;
            _contactCd = 0f;
            _attackCd = Mathf.Max(0f, _attackWindup);
            _attacking = false;
            _engaged = false;
            _attackIndex = 0;
            Phase = 1;
            _telegraphWait = new WaitForSeconds(Mathf.Max(0.05f, _telegraphTime));
            HealthChanged?.Invoke(_hp, MaxHp);
        }

        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _hp <= 0)
            {
                return false;
            }

            _hp -= amount;
            HealthChanged?.Invoke(Mathf.Max(0, _hp), MaxHp);

            if (!_engaged)
            {
                _engaged = true;                             // 첫 피격 → 공격 개시
                _attackCd = Mathf.Max(0.1f, _attackWindup);  // 첫 공격까지 잠깐 여유
            }

            if (Phase == 1 && _hp > 0 && _hp * 2 <= MaxHp)
            {
                Phase = 2;
                SfxPlayer.Play(SfxId.BossPhase);
                PhaseChanged?.Invoke(2);
            }

            if (_hp <= 0)
            {
                RunWallet.Instance?.Add(CurrencyType.Coin, _coinReward);
                SfxPlayer.Play(SfxId.BossDeath);
                Killed?.Invoke(AnchorCol, AnchorRow, _coinReward);
                Map.ClearEntity(this);
                // TODO: 클리어 연출 / 하강 재개는 GameManager 측에서 Killed 구독
                return true;
            }
            return false;
        }

        private void Update()
        {
            if (Map == null || _hp <= 0)
            {
                return;
            }

            PlayerController pc = Map.Player;
            bool playerOk = pc != null && pc.IsAlive;

            // 접촉 피해
            if (_contactCd > 0f)
            {
                _contactCd -= Time.deltaTime;
            }
            else if (playerOk && IsPlayerAdjacent(pc.Column, pc.Row))
            {
                pc.Damage(_contactDamage, pc.transform.position);
                _contactCd = _contactInterval;
            }

            // 공격 패턴 — 플레이어가 처음 때린 뒤부터
            if (_engaged && !_attacking)
            {
                _attackCd -= Time.deltaTime;
                if (_attackCd <= 0f)
                {
                    _attackCd = Mathf.Max(0.1f, _attackInterval);
                    if (playerOk)
                    {
                        StartCoroutine(AttackRoutine(ChooseAttack(), pc));
                    }
                }
            }
        }

        /// <summary>다음 공격 ID. 기본은 세로/가로 직선 번갈아. Boss2 는 오버라이드로 순서/비중 변경.</summary>
        protected virtual int ChooseAttack()
        {
            return (_attackIndex++ % 2 == 0) ? AttackColumnStrike : AttackRowStrike;
        }

        private IEnumerator AttackRoutine(int attackId, PlayerController pc)
        {
            _attacking = true;
            Debug.Log($"[Boss] attack id={attackId} phase={Phase}", this);

            _cells.Clear();
            BuildAttackCells(attackId, pc.Column, pc.Row, _cells);
            AttackTelegraph?.Invoke(_cells, _telegraphTime);

            yield return _telegraphWait;

            if (_hp > 0)
            {
                // 강타 사운드는 BossAttackVisual 이 충격파 칸마다 BossShockwave 로 낸다.
                // 파이어볼/토네이도 등 서브클래스 고유음은 OnAttackStrike 안에서.
                AttackStrike?.Invoke(_cells);
                OnAttackStrike(attackId, _cells);
            }

            _attacking = false;
        }

        /// <summary>
        /// 공격 ID 에 해당하는 대상 칸을 <paramref name="cells"/> 에 채운다.
        /// 서브클래스는 자기 ID 를 처리하고, 모르는 ID 는 base 로 넘긴다.
        /// </summary>
        protected virtual void BuildAttackCells(int attackId, int pcol, int prow, List<Vector2Int> cells)
        {
            if (attackId == AttackRowStrike)
            {
                // 격자 가로 한 줄을 듬성듬성 (oxoxo...). 매번 시작 칸을 랜덤으로 → 안전지대가 바뀜
                int off = UnityEngine.Random.Range(0, 2);
                for (int c = off; c < MapGenerator.Columns; c += 2)
                {
                    cells.Add(new Vector2Int(c, prow));
                }
            }
            else // AttackColumnStrike — 보스 머리 위에서부터 위로 _columnLength 칸 (충격파는 아래→위)
            {
                int bottom = AnchorRow - 1;
                for (int i = 0; i < _columnLength; i++)
                {
                    int r = bottom - i;
                    if (r < 0)
                    {
                        break;
                    }
                    cells.Add(new Vector2Int(pcol, r));
                }
            }
        }

        /// <summary>
        /// 강타 발동. 기본: 대상 칸에 플레이어가 있으면 피해만. <b>지층·일반 적은 건드리지 않는다.</b>
        /// 서브클래스는 자기 ID 를 다르게 처리하고, 모르는 ID 는 base 로 넘긴다.
        /// </summary>
        protected virtual void OnAttackStrike(int attackId, List<Vector2Int> cells)
        {
            PlayerController pc = Map != null ? Map.Player : null;
            if (pc == null || !pc.IsAlive)
            {
                return;
            }

            foreach (Vector2Int cell in cells)
            {
                if (pc.Column == cell.x && pc.Row == cell.y)
                {
                    pc.Damage(_attackDamage, pc.transform.position);
                    return;
                }
            }
        }
    }
}
