using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Map;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 보스를 <b>처음 때린 뒤부터</b> 상단에 HP 바를 띄우고, 처치되면 숨긴다.
    /// <see cref="MapGenerator.EntitySpawned"/> 를 구독해 <see cref="Boss"/> 를 자동으로 잡는다.
    ///
    /// 배치: 항상 활성인 UI 오브젝트에 붙이고 _group(CanvasGroup) 알파로 표시 토글.
    /// </summary>
    public class BossHud : MonoBehaviour
    {
        [SerializeField] private MapGenerator _map;
        [SerializeField] private CanvasGroup _group;
        [Tooltip("Image Type = Filled, Fill Method = Horizontal.")]
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private string _bossName = "BOSS";

        private Boss _boss;

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
            SetShown(false);
        }

        private void Start()
        {
            if (_map == null)
            {
                _map = FindFirstObjectByType<MapGenerator>();
            }
            if (_map != null)
            {
                _map.EntitySpawned += OnEntitySpawned;
            }
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.EntitySpawned -= OnEntitySpawned;
            }
            Unbind();
        }

        private void OnEntitySpawned(GridEntity e)
        {
            if (e is Boss boss)
            {
                Bind(boss);
            }
        }

        private void Bind(Boss boss)
        {
            Unbind();
            _boss = boss;
            _boss.HealthChanged += OnHealthChanged;
            _boss.Killed += OnKilled;
            if (_nameText != null)
            {
                _nameText.text = _bossName;
            }
            SetShown(false); // 첫 피격 전까지 숨김
        }

        private void Unbind()
        {
            if (_boss == null)
            {
                return;
            }
            _boss.HealthChanged -= OnHealthChanged;
            _boss.Killed -= OnKilled;
            _boss = null;
        }

        private void OnHealthChanged(int current, int max)
        {
            if (current < max) // 첫 피격 이후부터 표시
            {
                SetShown(true);
            }
            if (_fill != null)
            {
                _fill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            }
        }

        private void OnKilled(int col, int row, int coin)
        {
            SetShown(false);
            Unbind();
        }

        private void SetShown(bool visible)
        {
            if (_group == null)
            {
                return;
            }
            _group.alpha = visible ? 1f : 0f;
        }
    }
}
