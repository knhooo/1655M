using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 보호막이 켜지면 링(자식 오브젝트)을 표시. 플레이어 Visual 하위 아무 데나 붙인다.
    /// </summary>
    public class ShieldVisual : MonoBehaviour
    {
        [Tooltip("보호막 링 오브젝트 (스프라이트/파티클 등).")]
        [SerializeField] private GameObject _ring;

        private void Start()
        {
            if (_ring != null)
            {
                _ring.SetActive(false);
            }
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ShieldChanged += OnShieldChanged;
                _ring?.SetActive(PlayerController.Instance.ShieldActive);
            }
        }

        private void OnDestroy()
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ShieldChanged -= OnShieldChanged;
            }
        }

        private void OnShieldChanged(bool on)
        {
            if (_ring != null)
            {
                _ring.SetActive(on);
            }
        }
    }
}
