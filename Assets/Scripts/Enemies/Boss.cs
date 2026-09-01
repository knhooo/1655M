using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;

namespace Game.Enemies
{
    /// <summary>
    /// 보스. 격자에 <b>9x9</b> 로 박혀 제자리에서 공격. 파괴될 때까지 진로를 완전히 막는다(가로 전폭).
    ///  - 붙어 있으면 <see cref="_contactInterval"/> 초마다 접촉 피해 (등장 즉시)
    ///  - <b>플레이어가 처음 때린 뒤부터</b> <see cref="_attackInterval"/> 마다 패턴 1회: 텔레그래프(경고) → 딜레이 → 강타
    ///     · ColumnStrike : 플레이어가 선 세로 라인
    ///     · RadialBurst  : 플레이어 주변 원형
    ///  - 패턴 선택은 <see cref="ChooseAttack"/> 오버라이드로 바꿀 수 있다 (보스 B 리스킨용).
    ///
    /// 프리팹: GridEntity 의 Size 를 (9,9) 로. 스폰은 <see cref="MapGenerator"/> 의 _bossPrefab/_bossRow.
    /// </summary>
    public class Boss : GridEntity
    {
        public enum BossAttack { ColumnStrike, RadialBurst }

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
        [Tooltip("강타가 지층 블록에 주는 피해 (0 = 안 부숨).")]
        [SerializeField] private int _blockDamage = 999;
        [Tooltip("ColumnStrike 세로 사거리(플레이어 행 ± 이 값).")]
        [SerializeField, Min(1)] private int _columnReach = 2;
        [Tooltip("RadialBurst 반경(칸).")]
        [SerializeField, Min(1)] private int _burstRadius = 2;

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

        public int MaxHp => Mathf.Max(1, _maxHp);
        public int Hp => _hp;
        public bool IsAlive => _hp > 0;

        private int _hp;
        private float _contactCd;
        private float _attackCd;
        private bool _attacking;
        private bool _engaged;   // 플레이어가 처음 때린 뒤부터 공격 시작
        private int _attackIndex;

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

            if (_hp <= 0)
            {
                RunWallet.Instance?.Add(CurrencyType.Coin, _coinReward);
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

        /// <summary>다음 공격 선택. 기본은 번갈아. 보스 B 는 오버라이드로 순서/비중 변경.</summary>
        protected virtual BossAttack ChooseAttack()
        {
            return (_attackIndex++ % 2 == 0) ? BossAttack.ColumnStrike : BossAttack.RadialBurst;
        }

        private IEnumerator AttackRoutine(BossAttack attack, PlayerController pc)
        {
            _attacking = true;

            BuildCells(attack, pc.Column, pc.Row);
            AttackTelegraph?.Invoke(_cells, _telegraphTime);

            yield return _telegraphWait;

            if (_hp > 0)
            {
                AttackStrike?.Invoke(_cells);
                ApplyStrike();
            }

            _attacking = false;
        }

        private void BuildCells(BossAttack attack, int pcol, int prow)
        {
            _cells.Clear();

            if (attack == BossAttack.ColumnStrike)
            {
                int lo = Mathf.Max(0, prow - _columnReach);
                int hi = prow + _columnReach;
                for (int r = lo; r <= hi; r++)
                {
                    _cells.Add(new Vector2Int(pcol, r));
                }
            }
            else // RadialBurst
            {
                int r2 = _burstRadius * _burstRadius;
                for (int dr = -_burstRadius; dr <= _burstRadius; dr++)
                {
                    for (int dc = -_burstRadius; dc <= _burstRadius; dc++)
                    {
                        if (dc * dc + dr * dr > r2)
                        {
                            continue;
                        }
                        int c = pcol + dc;
                        int r = prow + dr;
                        if (c >= 0 && c < MapGenerator.Columns && r >= 0)
                        {
                            _cells.Add(new Vector2Int(c, r));
                        }
                    }
                }
            }
        }

        private void ApplyStrike()
        {
            PlayerController pc = Map != null ? Map.Player : null;
            bool hitPlayer = false;

            foreach (Vector2Int cell in _cells)
            {
                // 보스 자기 칸은 건드리지 않음 (self-damage 방지)
                if (_blockDamage > 0 && !CoversCell(cell.x, cell.y))
                {
                    Map.DamageCell(cell.x, cell.y, _blockDamage);
                }
                if (!hitPlayer && pc != null && pc.IsAlive && pc.Column == cell.x && pc.Row == cell.y)
                {
                    hitPlayer = true;
                }
            }

            if (hitPlayer)
            {
                pc.Damage(_attackDamage, pc.transform.position);
            }
        }
    }
}
