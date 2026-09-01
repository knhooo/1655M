using System.Collections.Generic;
using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Audio;

namespace Game.Enemies
{
    /// <summary>
    /// 두 번째(최종) 보스. <see cref="Boss"/> 를 상속. 고유 기믹 2종을 번갈아 쓴다.
    ///  - <b>Fireball</b> : 보스 머리에서 대각선 위로 파이어볼. 사각 범위 안에서 네 벽에 튕기며 왔다갔다.
    ///       · 페이즈 1 : 왼쪽/오른쪽 중 한 방향만 (번갈아)
    ///       · 페이즈 2 (HP 절반↓) : 양쪽 동시
    ///  - <b>Tornado</b> : 세로 라인 경고 → 거기 있으면 붕 떴다가 내려온다 (조작 불가 + 피해).
    ///       · 페이즈 1 : 플레이어 라인 1개
    ///       · 페이즈 2 : 여러 라인 동시
    ///
    /// 토네이도 프리팹은 <b>선택</b> — 없으면 BossAttackVisual 경고 마커로만 표현된다.
    /// 프리팹: Boss 프리팹 복제 → Boss 컴포넌트를 Boss2 로 교체, _fireballPrefab 연결, HP 상향.
    /// </summary>
    public class Boss2 : Boss
    {
        private const int AttackFireball = 2;
        private const int AttackTornado = 3;

        [Header("Boss 2 — Fireball")]
        [SerializeField] private Fireball _fireballPrefab;
        [Tooltip("파이어볼이 튕길 상단 높이 (보스 머리 위 월드 단위).")]
        [SerializeField] private float _fireballCeiling = 6f;
        [Tooltip("대각선 각도 (도). 45 = 정확히 45도 위.")]
        [SerializeField, Range(15f, 80f)] private float _fireballAngle = 55f;

        [Header("Boss 2 — Tornado")]
        [Tooltip("선택. 각 토네이도 라인에 띄울 연출 오브젝트. 없어도 됨(경고 마커로 대체).")]
        [SerializeField] private GameObject _tornadoPrefab;
        [SerializeField] private int _tornadoDamage = 30;
        [SerializeField] private float _tornadoLiftHeight = 2.5f;
        [Tooltip("플레이어가 붕 떠 있는 시간(초).")]
        [SerializeField] private float _tornadoDuration = 1.3f;
        [Tooltip("토네이도 연출 오브젝트가 남아 있는 시간(초). 리프트보다 길어도 됨.")]
        [SerializeField] private float _tornadoVfxLifetime = 3f;
        [Tooltip("경고할 세로 라인 길이(플레이어 행 -1 ~ +이 값).")]
        [SerializeField, Min(1)] private int _tornadoReach = 4;
        [Tooltip("페이즈 2에서 동시에 뜨는 토네이도 수.")]
        [SerializeField, Min(1)] private int _phase2TornadoCount = 3;

        private bool _fireballRight;                 // 페이즈 1 방향 토글
        private readonly List<int> _tornadoCols = new();

        // ------------------------------------------------------------------

        protected override int ChooseAttack()
        {
            return (_attackIndex++ % 2 == 0) ? AttackFireball : AttackTornado;
        }

        protected override void BuildAttackCells(int attackId, int pcol, int prow, List<Vector2Int> cells)
        {
            if (attackId == AttackFireball)
            {
                int hc = AnchorCol + Size.x / 2;
                int hr = AnchorRow;
                for (int step = 1; step <= 4; step++)
                {
                    int r = hr - step;
                    if (r < 0) break;
                    bool both = Phase >= 2;
                    if (both || !_fireballRight) AddCell(cells, hc - step, r);
                    if (both || _fireballRight) AddCell(cells, hc + step, r);
                }
            }
            else if (attackId == AttackTornado)
            {
                BuildTornadoColumns(pcol);
                int lo = Mathf.Max(0, prow - 1);
                int hi = prow + _tornadoReach;
                foreach (int c in _tornadoCols)
                {
                    for (int r = lo; r <= hi; r++)
                    {
                        cells.Add(new Vector2Int(c, r));
                    }
                }
            }
            else
            {
                base.BuildAttackCells(attackId, pcol, prow, cells);
            }
        }

        protected override void OnAttackStrike(int attackId, List<Vector2Int> cells)
        {
            if (attackId == AttackFireball)
            {
                LaunchFireball();
            }
            else if (attackId == AttackTornado)
            {
                DoTornado();
            }
            else
            {
                base.OnAttackStrike(attackId, cells);
            }
        }

        // ------------------------------------------------------------------
        // Fireball
        // ------------------------------------------------------------------

        private void LaunchFireball()
        {
            if (_fireballPrefab == null || Map == null)
            {
                return;
            }

            SfxPlayer.Play(SfxId.BossFireball);

            float half = Map.CellSize * 0.5f;
            int hr = AnchorRow;
            float minX = Map.CellToWorld(0, hr).x - half;
            float maxX = Map.CellToWorld(MapGenerator.Columns - 1, hr).x + half;

            Vector3 head = transform.position + Vector3.up * (Size.y * 0.5f * Map.CellSize);
            float minY = head.y - half;
            float maxY = head.y + _fireballCeiling;

            float ang = _fireballAngle * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 left = new Vector2(-right.x, right.y);

            if (Phase >= 2)
            {
                Spawn(head, left, minX, maxX, minY, maxY);
                Spawn(head, right, minX, maxX, minY, maxY);
            }
            else
            {
                Spawn(head, _fireballRight ? right : left, minX, maxX, minY, maxY);
                _fireballRight = !_fireballRight;
            }
        }

        private void Spawn(Vector3 pos, Vector2 dir, float minX, float maxX, float minY, float maxY)
        {
            Instantiate(_fireballPrefab).Launch(pos, dir, minX, maxX, minY, maxY);
        }

        // ------------------------------------------------------------------
        // Tornado
        // ------------------------------------------------------------------

        private void BuildTornadoColumns(int pcol)
        {
            _tornadoCols.Clear();
            _tornadoCols.Add(Mathf.Clamp(pcol, 0, MapGenerator.Columns - 1));

            if (Phase >= 2)
            {
                int want = Mathf.Min(_phase2TornadoCount, MapGenerator.Columns);
                int step = Mathf.Max(1, MapGenerator.Columns / want);
                for (int c = 0; c < MapGenerator.Columns && _tornadoCols.Count < want; c += step)
                {
                    if (!_tornadoCols.Contains(c))
                    {
                        _tornadoCols.Add(c);
                    }
                }
            }
        }

        private void DoTornado()
        {
            PlayerController pc = Map != null ? Map.Player : null;

            if (_tornadoCols.Count > 0)
            {
                SfxPlayer.Play(SfxId.BossTornado);
            }

            foreach (int c in _tornadoCols)
            {
                if (_tornadoPrefab != null && Map != null)
                {
                    float life = Mathf.Max(_tornadoDuration, _tornadoVfxLifetime);
                    GameObject vfx = Instantiate(_tornadoPrefab,
                        Map.CellToWorld(c, Mathf.Max(0, AnchorRow - 1)), Quaternion.identity);
                    if (vfx.TryGetComponent(out TransientVfx tv))
                    {
                        tv.Play(life); // 작게 → 커졌다 → 작아지며 사라짐
                    }
                    else
                    {
                        Destroy(vfx, life);
                    }
                }
            }

            if (pc != null && pc.IsAlive && _tornadoCols.Contains(pc.Column))
            {
                pc.Levitate(_tornadoLiftHeight, _tornadoDuration, _tornadoDamage, transform.position);
            }
        }

        // ------------------------------------------------------------------

        private static void AddCell(List<Vector2Int> cells, int c, int r)
        {
            if (c >= 0 && c < MapGenerator.Columns && r >= 0)
            {
                cells.Add(new Vector2Int(c, r));
            }
        }
    }
}
