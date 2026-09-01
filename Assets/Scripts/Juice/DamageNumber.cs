using System;
using TMPro;
using UnityEngine;

namespace Game.Juice
{
    /// <summary>
    /// 풀링되는 플로팅 데미지 텍스트. 두 가지 모션:
    ///  - RiseFade : 위로 곧게 올라가며 페이드아웃 (플레이어 피격)
    ///  - PopFall  : 무작위로 튀어올랐다 중력으로 낙하, 화면 밖에서 소멸 (적 피격)
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumber : MonoBehaviour
    {
        public enum Motion { RiseFade, PopFall }

        [SerializeField] private TextMeshPro _text;

        [Header("RiseFade (플레이어 피격)")]
        [SerializeField] private float _riseSpeed = 2.2f;
        [SerializeField] private float _riseLifetime = 0.7f;

        [Header("PopFall (적 피격)")]
        [SerializeField] private float _popUpSpeedMin = 3f;
        [SerializeField] private float _popUpSpeedMax = 5.5f;
        [SerializeField] private float _popSideSpeed = 2f;
        [SerializeField] private float _gravity = 14f;
        [SerializeField] private float _popMaxLifetime = 1.6f;

        public event Action<DamageNumber> Finished;

        private Motion _motion;
        private Vector3 _velocity;
        private float _life;
        private Camera _cam;

        private void Reset()
        {
            _text = GetComponent<TextMeshPro>();
        }

        public void Play(Vector3 worldPos, string label, Color color, Motion motion)
        {
            if (_text == null)
            {
                _text = GetComponent<TextMeshPro>();
            }
            if (_cam == null)
            {
                _cam = Camera.main;
            }

            transform.position = worldPos;
            transform.rotation = Quaternion.identity; // 2D 고정 카메라 — 빌보드 불필요
            _text.alignment = TextAlignmentOptions.Center; // transform 위치에 정확히 중앙 정렬
            _text.text = label;
            _text.color = color;
            _text.alpha = 1f;

            _motion = motion;
            _life = 0f;
            _velocity = motion == Motion.PopFall
                ? new Vector3(
                    UnityEngine.Random.Range(-_popSideSpeed, _popSideSpeed),
                    UnityEngine.Random.Range(_popUpSpeedMin, _popUpSpeedMax),
                    0f)
                : Vector3.up * _riseSpeed;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _life += Time.deltaTime;

            if (_motion == Motion.RiseFade)
            {
                transform.position += _velocity * Time.deltaTime;
                _text.alpha = Mathf.Clamp01(1f - _life / _riseLifetime);
                if (_life >= _riseLifetime)
                {
                    End();
                }
                return;
            }

            // PopFall
            _velocity.y -= _gravity * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;

            if (_life >= _popMaxLifetime || IsBelowScreen())
            {
                End();
            }
        }

        private bool IsBelowScreen()
        {
            if (_cam == null)
            {
                return false;
            }
            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.y < -0.05f;
        }

        private void End()
        {
            gameObject.SetActive(false);
            Finished?.Invoke(this);
        }
    }
}
