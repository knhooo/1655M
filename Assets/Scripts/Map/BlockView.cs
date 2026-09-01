using System;
using UnityEngine;

namespace Game.Map
{
    /// <summary>
    /// 풀링되는 지층 블록 1칸의 시각 표현.
    /// 스스로 상태를 갖지 않고, <see cref="MapGenerator"/>가 넘겨주는 <see cref="BlockData"/>를 따른다.
    /// 지층 타입별 스프라이트는 <see cref="_typeSprites"/> 에 등록 (프리팹 하나로 전 타입 처리).
    ///
    /// 파괴될 때 <see cref="PlayDestroyToss"/> 로 위로 튕겼다가 떨어지는 연출을 하고,
    /// 화면 밖으로 나가면 콜백(풀 반납)을 부른다. 이 동안엔 격자에서 이미 분리된 상태.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BlockView : MonoBehaviour
    {
        [Serializable]
        private struct TypeSprite
        {
            public StrataType type;
            public Sprite sprite;
        }

        [SerializeField] private SpriteRenderer _renderer;

        [Header("지층 타입별 스프라이트")]
        [SerializeField] private TypeSprite[] _typeSprites;

        [Header("파괴 연출 (튕김)")]
        [Tooltip("파괴 시 튕겨 날아가는 연출을 할지. 끄면 즉시 사라짐.")]
        [SerializeField] private bool _tossOnDestroy = true;
        [SerializeField] private float _upSpeedMin = 2.5f;
        [SerializeField] private float _upSpeedMax = 5f;
        [SerializeField] private float _sideSpeed = 2.5f;
        [SerializeField] private float _gravity = 22f;
        [SerializeField] private float _spinSpeed = 300f;
        [Tooltip("화면 안이어도 이 시간 지나면 종료(안전장치).")]
        [SerializeField] private float _tossMaxLifetime = 3f;
        [Tooltip("튕기는 동안 sorting order 를 이만큼 올려 남은 지형 위로 보이게.")]
        [SerializeField] private int _tossSortingBoost = 20;

        public int Col { get; private set; }
        public int Row { get; private set; }
        public bool TossOnDestroy => _tossOnDestroy;

        private Camera _cam;
        private int _baseSortingOrder;
        private bool _baseSortingCaptured;
        private Vector3 _tossVel;
        private float _tossSpin;
        private float _tossLife;
        private Action _tossDone;
        private bool _tossing;

        private void Reset()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>풀에서 꺼내 특정 칸에 배치할 때 호출.</summary>
        public void Bind(int col, int row, BlockData data)
        {
            Col = col;
            Row = row;
            name = $"Block_{col}_{row}";

            _tossing = false;
            _tossDone = null;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (_baseSortingCaptured && _renderer != null)
            {
                _renderer.sortingOrder = _baseSortingOrder;
            }

            Refresh(data);
        }

        /// <summary>피해를 받았지만 파괴되지는 않았을 때.</summary>
        public void OnDamaged(BlockData data)
        {
            Refresh(data);
            // TODO: 크랙 스프라이트 단계 / 히트 플래시
        }

        /// <summary>
        /// 파괴 연출 시작. 격자에서 분리된 뒤 호출. 위로 튕겼다가 낙하 → 화면 밖에서 <paramref name="onFinished"/>.
        /// </summary>
        public void PlayDestroyToss(Action onFinished)
        {
            _tossDone = onFinished;
            if (_cam == null)
            {
                _cam = Camera.main;
            }
            if (_renderer != null)
            {
                if (!_baseSortingCaptured)
                {
                    _baseSortingOrder = _renderer.sortingOrder;
                    _baseSortingCaptured = true;
                }
                _renderer.sortingOrder = _baseSortingOrder + _tossSortingBoost;
            }
            _tossVel = new Vector3(
                UnityEngine.Random.Range(-_sideSpeed, _sideSpeed),
                UnityEngine.Random.Range(_upSpeedMin, _upSpeedMax),
                0f);
            _tossSpin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * _spinSpeed;
            _tossLife = 0f;
            _tossing = true;
        }

        private void Update()
        {
            if (!_tossing)
            {
                return;
            }

            _tossLife += Time.deltaTime;
            _tossVel.y -= _gravity * Time.deltaTime;
            transform.position += _tossVel * Time.deltaTime;
            transform.Rotate(0f, 0f, _tossSpin * Time.deltaTime);

            if (_tossLife >= _tossMaxLifetime || IsOffScreen())
            {
                _tossing = false;
                Action cb = _tossDone;
                _tossDone = null;
                cb?.Invoke(); // MapGenerator 가 풀에 반납
            }
        }

        private void OnDisable()
        {
            _tossing = false;
            _tossDone = null;
        }

        private bool IsOffScreen()
        {
            if (_cam == null)
            {
                return false;
            }
            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            return vp.z <= 0f || vp.y < -0.25f || vp.x < -0.3f || vp.x > 1.3f;
        }

        private void Refresh(BlockData data)
        {
            if (_renderer == null)
            {
                return;
            }

            Sprite s = SpriteFor(data.Type);
            if (s != null)
            {
                _renderer.sprite = s;
            }
        }

        private Sprite SpriteFor(StrataType type)
        {
            if (_typeSprites != null)
            {
                foreach (TypeSprite ts in _typeSprites)
                {
                    if (ts.type == type && ts.sprite != null)
                    {
                        return ts.sprite;
                    }
                }
            }
            return null;
        }
    }
}
