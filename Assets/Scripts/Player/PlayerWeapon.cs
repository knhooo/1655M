using System.Collections;
using UnityEngine;
using Game.Equipment;

namespace Game.Player
{
    /// <summary>
    /// 플레이어가 장착한 무기 표시 + 공격 시 부채꼴로 휘두르기 + 슬래시 이펙트.
    ///
    /// 권장 구조 (Player 하위):
    ///   Player
    ///   ├─ WeaponAnchor            (플레이어 중심, 회전 0) — _weapon 의 부모
    ///   │  └─ Weapon (SR)          ← _weapon
    ///   └─ SlashEffect (SR)        ← _effect (기본 비활성)
    ///
    /// 스윙 중에는 _weapon 을 앵커 중심의 원호 위에서 **방사형**(자루=안쪽, 칼끝=바깥)으로 돌린다.
    /// 대기 자세 = 에디터에서 배치한 _weapon 의 초기 localPosition/localRotation. 공격이 끝나면 그리로 복귀.
    /// 스프라이트는 <see cref="SwordIconSet"/> 에서 <see cref="PlayerStats.EquippedSword"/> 로 가져온다.
    /// </summary>
    public class PlayerWeapon : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private SpriteRenderer _weapon;
        [Tooltip("슬래시 이펙트. 기본 비활성. 스윙에 딸려 돌지 않는 오브젝트.")]
        [SerializeField] private SpriteRenderer _effect;
        [SerializeField] private SwordIconSet _iconSet;

        [Header("방향 보정")]
        [Tooltip("무기 스프라이트가 회전 0에서 '칼끝이 가리키는 방향'을 오른쪽(+X)으로 맞추는 각도.\n" +
                 "칼끝이 위면 -90, 아래면 90, 왼쪽이면 180.")]
        [SerializeField] private float _spriteForwardOffset = 0f;
        [Tooltip("왼쪽을 향할 때 무기 상하 반전(칼등이 뒤집히지 않게).")]
        [SerializeField] private bool _flipYWhenFacingLeft = true;

        [Header("휘두르기")]
        [Tooltip("스윙할 때 앵커 중심에서 무기까지의 반지름. 0이면 배치한 위치의 거리를 사용.")]
        [SerializeField] private float _swingRadius = 0.9f;
        [Tooltip("부채꼴 각도(도).")]
        [SerializeField] private float _arc = 130f;
        [Tooltip("휘두르는 시간(초).")]
        [SerializeField] private float _swingDuration = 0.13f;

        [Header("이펙트")]
        [SerializeField] private float _effectDuration = 0.12f;
        [SerializeField] private float _effectStartScale = 0.6f;
        [SerializeField] private float _effectEndScale = 1.15f;
        [Tooltip("이펙트 스프라이트가 기본에서 가리키는 방향을 오른쪽(+X)으로 맞추는 각도.")]
        [SerializeField] private float _effectForwardOffset = 0f;
        [Tooltip("이펙트를 페이드아웃할지.")]
        [SerializeField] private bool _effectFade = true;

        private Sprite _effectSprite;
        private float _weaponScale = 1f;
        private float _effectScale = 1f;
        private Vector3 _weaponBaseScale = Vector3.one;
        private Coroutine _swing;

        // 초기(대기) 상태 - Awake 에서 기억
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private bool _homeFlipY;
        private float _radius = 0.9f;

        private void Awake()
        {
            if (_weapon != null)
            {
                _weaponBaseScale = _weapon.transform.localScale;
                _homePos = _weapon.transform.localPosition;
                _homeRot = _weapon.transform.localRotation;
                _homeFlipY = _weapon.flipY;

                float placed = new Vector2(_homePos.x, _homePos.y).magnitude;
                _radius = _swingRadius > 0f ? _swingRadius : (placed > 0.01f ? placed : 0.5f);
            }

            if (_effect != null)
            {
                _effect.enabled = false;
            }
        }

        private void Start()
        {
            RefreshEquipped();
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.Attacked += OnAttacked;
            }
            PlayerStats.Changed += RefreshEquipped;
        }

        private void OnDestroy()
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.Attacked -= OnAttacked;
            }
            PlayerStats.Changed -= RefreshEquipped;
        }

        private void RefreshEquipped()
        {
            SwordRarity? r = PlayerStats.EquippedSword;
            if (_iconSet != null && r.HasValue)
            {
                if (_weapon != null)
                {
                    Sprite s = _iconSet.Get(r.Value);
                    if (s != null)
                    {
                        _weapon.sprite = s;
                    }
                }
                _effectSprite = _iconSet.GetEffect(r.Value);
                _weaponScale = _iconSet.GetSpriteScale(r.Value);
                _effectScale = _iconSet.GetEffectScale(r.Value);
            }
            else
            {
                _weaponScale = 1f;
                _effectScale = 1f;
                if (_effect != null)
                {
                    _effectSprite = _effect.sprite;
                }
            }

            if (_weapon != null)
            {
                _weapon.transform.localScale = _weaponBaseScale * _weaponScale;
            }
        }

        private void OnAttacked(int dCol, int dRow)
        {
            Vector2 dir = new Vector2(dCol, -dRow); // dRow +1 = 아래
            float center = dir.sqrMagnitude > 0.001f
                ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg
                : 0f;

            if (_swing != null)
            {
                StopCoroutine(_swing);
            }
            _swing = StartCoroutine(SwingRoutine(center));
        }

        private IEnumerator SwingRoutine(float center)
        {
            bool faceLeft = _flipYWhenFacingLeft && Mathf.Abs(Mathf.DeltaAngle(center, 180f)) < 90f;
            if (_weapon != null)
            {
                _weapon.flipY = faceLeft;
            }

            PlayEffect(center, faceLeft);

            // 정면 스윙 = 위 → 아래. 왼쪽을 볼 땐 미러링 되도록 회전 방향도 뒤집는다.
            float dirSign = faceLeft ? -1f : 1f;
            float from = center + dirSign * _arc * 0.5f;
            float to = center - dirSign * _arc * 0.5f;

            float dur = Mathf.Max(0.01f, _swingDuration);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                SetRadialPose(Mathf.LerpAngle(from, to, k));
                yield return null;
            }

            RestoreHome(); // 스윙 끝나면 회전 애니메이션 없이 즉시 초기 위치로
            _swing = null;
        }

        /// <summary>무기를 앵커 중심 원호 위에 놓고 칼끝이 바깥(각도 방향)을 향하게 한다.</summary>
        private void SetRadialPose(float angleDeg)
        {
            if (_weapon == null)
            {
                return;
            }
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _radius;
            p.z = _homePos.z;
            _weapon.transform.localPosition = p;
            _weapon.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg + _spriteForwardOffset);
        }

        private void RestoreHome()
        {
            if (_weapon == null)
            {
                return;
            }
            _weapon.transform.localPosition = _homePos;
            _weapon.transform.localRotation = _homeRot;
            _weapon.flipY = _homeFlipY;
        }

        private void PlayEffect(float center, bool faceLeft)
        {
            if (_effect == null)
            {
                return;
            }
            if (_effectSprite != null)
            {
                _effect.sprite = _effectSprite;
            }
            _effect.flipY = faceLeft;
            _effect.transform.localRotation = Quaternion.Euler(0f, 0f, center + _effectForwardOffset);
            _effect.enabled = true;
            StartCoroutine(EffectRoutine());
        }

        private IEnumerator EffectRoutine()
        {
            Color baseColor = _effect.color;
            float dur = Mathf.Max(0.01f, _effectDuration);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);

                float s = Mathf.Lerp(_effectStartScale, _effectEndScale, k) * _effectScale;
                _effect.transform.localScale = new Vector3(s, s, 1f);

                if (_effectFade)
                {
                    Color c = baseColor;
                    c.a = baseColor.a * (1f - k);
                    _effect.color = c;
                }
                yield return null;
            }
            _effect.color = baseColor;
            _effect.enabled = false;
        }
    }
}
