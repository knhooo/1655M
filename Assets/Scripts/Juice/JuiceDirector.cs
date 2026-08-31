using UnityEngine;
using Game.Player;
using Game.Map;
using Game.CameraRig;

namespace Game.Juice
{
    /// <summary>
    /// 타격/피격 이벤트를 받아 히트스톱 + 카메라 흔들림을 낸다. 씬에 하나 (항상 활성).
    ///  - 플레이어 피격 : 큰 히트스톱 + 큰 흔들림
    ///  - 치명타(플레이어가 줌) : 작은 히트스톱 + 중간 흔들림
    ///  - 일반 적 타격 : 아주 작은 흔들림만
    ///  - 충격파 : 중간 흔들림 + 작은 히트스톱
    /// 지층 채굴(치명타 아님)은 아무 효과 없음.
    /// </summary>
    public class JuiceDirector : MonoBehaviour
    {
        [SerializeField] private CameraFollow _camera;
        [SerializeField] private MapGenerator _map;

        [Header("플레이어 피격")]
        [SerializeField] private float _hurtHitStop = 0.09f;
        [SerializeField] private float _hurtShakeAmp = 0.10f;
        [SerializeField] private float _hurtShakeDur = 0.20f;

        [Header("치명타 (플레이어가 줌)")]
        [SerializeField] private float _critHitStop = 0.05f;
        [SerializeField] private float _critShakeAmp = 0.04f;
        [SerializeField] private float _critShakeDur = 0.12f;

        [Header("일반 적/엔티티 타격")]
        [SerializeField] private float _hitShakeAmp = 0.015f;
        [SerializeField] private float _hitShakeDur = 0.07f;

        [Header("충격파")]
        [SerializeField] private float _shockHitStop = 0.05f;
        [SerializeField] private float _shockShakeAmp = 0.08f;
        [SerializeField] private float _shockShakeDur = 0.18f;

        private void Reset()
        {
            _camera = FindFirstObjectByType<CameraFollow>();
            _map = FindFirstObjectByType<MapGenerator>();
        }

        // 구독은 Start 에서 (모든 Awake 이후 = 싱글톤 보장)
        private void Start()
        {
            if (_camera == null)
            {
                _camera = FindFirstObjectByType<CameraFollow>();
            }
            if (_map == null)
            {
                _map = FindFirstObjectByType<MapGenerator>();
            }

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.DamageTaken += OnPlayerDamaged;
                PlayerController.Instance.ShockwaveFired += OnShockwave;
            }
            if (_map != null)
            {
                _map.DamageDealt += OnDamageDealt;
            }
        }

        private void OnDestroy()
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.DamageTaken -= OnPlayerDamaged;
                PlayerController.Instance.ShockwaveFired -= OnShockwave;
            }
            if (_map != null)
            {
                _map.DamageDealt -= OnDamageDealt;
            }
        }

        private void OnPlayerDamaged(int amount, Vector3 source)
        {
            HitStop.Do(_hurtHitStop);
            Shake(_hurtShakeAmp, _hurtShakeDur);
        }

        private void OnDamageDealt(Vector3 worldPos, int amount, bool crit, bool isEntity)
        {
            if (crit)
            {
                HitStop.Do(_critHitStop);
                Shake(_critShakeAmp, _critShakeDur);
            }
            else if (isEntity)
            {
                Shake(_hitShakeAmp, _hitShakeDur);
            }
        }

        private void OnShockwave(int col, int row, int radius)
        {
            HitStop.Do(_shockHitStop);
            Shake(_shockShakeAmp, _shockShakeDur);
        }

        private void Shake(float amp, float dur)
        {
            if (_camera != null)
            {
                _camera.Shake(amp, dur);
            }
        }
    }
}
