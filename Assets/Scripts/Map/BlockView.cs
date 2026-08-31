using System;
using UnityEngine;

namespace Game.Map
{
    /// <summary>
    /// 풀링되는 지층 블록 1칸의 시각 표현.
    /// 스스로 상태를 갖지 않고, <see cref="MapGenerator"/>가 넘겨주는 <see cref="BlockData"/>를 따른다.
    /// 지층 타입별 스프라이트는 <see cref="_typeSprites"/> 에 등록 (프리팹 하나로 전 타입 처리).
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

        public int Col { get; private set; }
        public int Row { get; private set; }

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
            Refresh(data);
        }

        /// <summary>피해를 받았지만 파괴되지는 않았을 때.</summary>
        public void OnDamaged(BlockData data)
        {
            Refresh(data);
            // TODO: 크랙 스프라이트 단계 / 히트 플래시
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
