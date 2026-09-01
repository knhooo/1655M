using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 <see cref="Animator"/> 를 상황에 맞게 재생한다. 컨트롤러에 <b>Idle / attack / die</b>
    /// 세 상태가 있다고 가정하고, 파라미터 없이 <see cref="Animator.CrossFadeInFixedTime"/>(또는 Play)
    /// 로 상태를 직접 전환한다 (전이/트리거 설정 불필요).
    ///  - 스폰/대기 : Idle
    ///  - 공격 예고 → 강타 : attack  (경고 딜레이만큼 재생 후 Idle 복귀)
    ///  - 사망 : die  (<see cref="Boss"/> 가 <see cref="_deathAnimHold"/> 만큼 회수를 늦춤)
    ///
    /// 선택: <see cref="_body"/> 를 연결하면 피격 시 스프라이트 흰 플래시를 애니 위에 얹는다.
    /// 배치: 보스 프리팹 루트(또는 Animator 가 있는 자식)에.
    /// </summary>
    public class BossAnimation : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Animator _animator;
        [Tooltip("선택 — 연결 시 피격 흰 플래시. 보통 몸 SpriteRenderer.")]
        [SerializeField] private SpriteRenderer _body;

        [Header("애니 상태 이름 (컨트롤러와 일치해야 함)")]
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _attackState = "attack";
        [SerializeField] private string _dieState = "die";
        [Tooltip("상태 전환에 쓰는 크로스페이드 시간(초). 0 이면 즉시 Play.")]
        [SerializeField] private float _crossFade = 0.08f;

        [Header("공격")]
        [Tooltip("attack 재생을 예고(Telegraph) 시점에 시작한다. 끄면 강타(Strike) 시점.")]
        [SerializeField] private bool _attackOnTelegraph = true;
        [Tooltip("강타 이후 attack 를 더 유지하다 Idle 로 돌아가는 시간(초).")]
        [SerializeField] private float _attackTail = 0.35f;

        [Header("피격 플래시 (선택)")]
        [SerializeField] private bool _hitFlash = true;
        [SerializeField] private Color _hitFlashColor = Color.white;
        [SerializeField] private float _hitFlashTime = 0.09f;

        [Header("사망 이펙트 (선택)")]
        [Tooltip("사망 위치에 Instantiate 되는 폭발/파티클 프리팹. die 애니와 별개.")]
        [SerializeField] private GameObject _deathBurstPrefab;
        [SerializeField] private float _deathBurstLifetime = 3f;

        private Boss _boss;
        private int _lastHp = int.MaxValue;
        private bool _dead;
        private Color _homeColor = Color.white;

        private Coroutine _returnCo;
        private Coroutine _flashCo;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            _boss = GetComponent<Boss>();
            if (_boss == null) _boss = GetComponentInParent<Boss>();

            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            if (_body != null) _homeColor = _body.color;
        }

        private void OnEnable()
        {
            _lastHp = int.MaxValue;
            _dead = false;
            if (_body != null) _body.color = _homeColor;
            PlayState(_idleState);

            if (_boss != null)
            {
                _boss.HealthChanged += OnHealthChanged;
                _boss.AttackTelegraph += OnTelegraph;
                _boss.AttackStrike += OnStrike;
                _boss.Killed += OnKilled;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.HealthChanged -= OnHealthChanged;
                _boss.AttackTelegraph -= OnTelegraph;
                _boss.AttackStrike -= OnStrike;
                _boss.Killed -= OnKilled;
            }
            StopAllCoroutines();
            _returnCo = null;
            _flashCo = null;
        }

        // ------------------------------------------------------------------

        private void OnHealthChanged(int current, int max)
        {
            if (current < _lastHp && current > 0 && !_dead && _hitFlash && _body != null)
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                _flashCo = StartCoroutine(Flash());
            }
            _lastHp = current;
        }

        private void OnTelegraph(IReadOnlyList<Vector2Int> cells, float duration)
        {
            if (_dead || !_attackOnTelegraph)
            {
                return;
            }
            PlayAttack(duration + _attackTail);
        }

        private void OnStrike(IReadOnlyList<Vector2Int> cells)
        {
            if (_dead)
            {
                return;
            }
            if (_attackOnTelegraph)
            {
                // 예고 때 시작한 attack 를 강타 후 짧게 더 유지하고 Idle 복귀
                ScheduleReturn(_attackTail);
            }
            else
            {
                PlayAttack(_attackTail);
            }
        }

        private void OnKilled(int col, int row, int coin)
        {
            _dead = true;
            if (_returnCo != null) { StopCoroutine(_returnCo); _returnCo = null; }
            if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
            if (_body != null) _body.color = _homeColor;
            PlayState(_dieState);

            if (_deathBurstPrefab != null)
            {
                GameObject g = Instantiate(_deathBurstPrefab, transform.position, Quaternion.identity);
                g.SetActive(true);
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Play(true);
                }
                Destroy(g, Mathf.Max(0.1f, _deathBurstLifetime));
            }
        }

        // ------------------------------------------------------------------

        private void PlayAttack(float returnAfter)
        {
            PlayState(_attackState);
            ScheduleReturn(returnAfter);
        }

        private void ScheduleReturn(float delay)
        {
            if (_returnCo != null) StopCoroutine(_returnCo);
            _returnCo = StartCoroutine(ReturnToIdle(delay));
        }

        private IEnumerator ReturnToIdle(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            _returnCo = null;
            if (!_dead)
            {
                PlayState(_idleState);
            }
        }

        private void PlayState(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state))
            {
                return;
            }
            if (_crossFade > 0f)
            {
                _animator.CrossFadeInFixedTime(state, _crossFade);
            }
            else
            {
                _animator.Play(state, 0, 0f);
            }
        }

        private IEnumerator Flash()
        {
            float half = Mathf.Max(0.01f, _hitFlashTime) * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                _body.color = Color.Lerp(_homeColor, _hitFlashColor, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                _body.color = Color.Lerp(_hitFlashColor, _homeColor, t / half);
                yield return null;
            }
            _body.color = _homeColor;
            _flashCo = null;
        }
    }
}
