using UnityEngine;
using UnityEngine.Pool;
using Game.Player;
using Game.Map;

namespace Game.Juice
{
    /// <summary>
    /// 플레이어 피격 / 적 피격(일반·치명타) 데미지 텍스트를 생성한다.
    ///  - 플레이어 피격: 마젠타, 위로 곧게 올라가며 소멸
    ///  - 적 피격(일반): 그린, 튀어올랐다 낙하
    ///  - 적 피격(치명타): 빨강, 동일 모션
    /// 항상 활성인 오브젝트에 붙이고 _player / _map / _prefab 를 연결.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] private DamageNumber _prefab;
        [SerializeField] private PlayerController _player;
        [SerializeField] private MapGenerator _map;

        [Header("색상")]
        [SerializeField] private Color _playerHitColor = new Color(1f, 0f, 1f);       // 마젠타
        [SerializeField] private Color _enemyHitColor = new Color(0.35f, 1f, 0.4f);   // 그린
        [SerializeField] private Color _critColor = new Color(1f, 0.25f, 0.2f);       // 빨강

        [Tooltip("지층(기본 적) 타격에도 숫자를 띄울지. 끄면 Enemy/Chest 엔티티만.")]
        [SerializeField] private bool _showBlockDamage = true;

        [SerializeField] private int _prewarm = 24;

        private ObjectPool<DamageNumber> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<DamageNumber>(
                createFunc: () =>
                {
                    DamageNumber n = Instantiate(_prefab, transform);
                    n.gameObject.SetActive(false);
                    n.Finished += OnFinished;
                    return n;
                },
                actionOnGet: null,
                actionOnRelease: null,
                actionOnDestroy: n => { if (n != null) { Destroy(n.gameObject); } },
                collectionCheck: false,
                defaultCapacity: _prewarm,
                maxSize: 256);
        }

        private void OnEnable()
        {
            if (_player != null) _player.DamageTaken += OnPlayerDamaged;
            if (_map != null) _map.DamageDealt += OnDamageDealt;
        }

        private void OnDisable()
        {
            if (_player != null) _player.DamageTaken -= OnPlayerDamaged;
            if (_map != null) _map.DamageDealt -= OnDamageDealt;
        }

        private void OnPlayerDamaged(int amount, Vector3 sourcePos)
        {
            // 피해를 준 적/지층 위치에서 정확히 생성
            Spawn(sourcePos, $"-{amount}", _playerHitColor, DamageNumber.Motion.RiseFade);
        }

        private void OnDamageDealt(Vector3 worldPos, int amount, bool crit, bool isEntity)
        {
            if (!isEntity && !_showBlockDamage)
            {
                return;
            }
            Spawn(worldPos, amount.ToString(), crit ? _critColor : _enemyHitColor, DamageNumber.Motion.PopFall);
        }

        private void Spawn(Vector3 pos, string label, Color color, DamageNumber.Motion motion)
        {
            if (_prefab == null)
            {
                return;
            }
            _pool.Get().Play(pos, label, color, motion);
        }

        private void OnFinished(DamageNumber n) => _pool.Release(n);
    }
}
