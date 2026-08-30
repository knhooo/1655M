using UnityEngine;

namespace Game.Map
{
    /// <summary>
    /// 풀링되는 지층 블록 1칸의 시각 표현.
    /// 스스로 상태를 갖지 않고, <see cref="MapGenerator"/>가 넘겨주는 <see cref="BlockData"/>를 따른다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BlockView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;

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
            // TODO: 크랙 스프라이트 단계 표시 / 히트 플래시 / 흔들림
        }

        private void Refresh(BlockData data)
        {
            if (_renderer == null)
            {
                return;
            }

            // TODO: StrataType 별 스프라이트로 교체. 임시로 색만.
            Color baseColor = data.Type switch
            {
                StrataType.Soil => new Color(0.52f, 0.38f, 0.26f),
                StrataType.Rock => new Color(0.42f, 0.44f, 0.48f),
                StrataType.Ore  => new Color(0.30f, 0.55f, 0.62f),
                _ => Color.magenta,
            };

            // 피해를 받을수록 어둡게 (남은 HP 비율).
            float wear = Mathf.Lerp(0.55f, 1f, data.HpNormalized);
            _renderer.color = new Color(baseColor.r * wear, baseColor.g * wear, baseColor.b * wear, 1f);
        }
    }
}
