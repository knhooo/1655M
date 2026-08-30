using System;
using UnityEngine;

namespace Game.Map
{
    /// <summary>
    /// 격자에 지층 블록 대신 배치되는 오브젝트의 베이스 (적, 나중에 아이템 등).
    ///  - 1x1 또는 2x2(그 이상도 가능) 칸을 점유한다.
    ///  - 점유한 칸은 solid 취급되고, <see cref="MapGenerator.DamageCell"/> 는 이 엔티티로 전달된다.
    ///  - 이동하지 않는다. 스스로 사라질 땐 <see cref="MapGenerator.ClearEntity"/> 를 호출.
    /// 프리팹 단위로 풀링된다.
    /// </summary>
    public abstract class GridEntity : MonoBehaviour, IDamageable
    {
        [Tooltip("점유하는 칸 크기. (1,1) = 한 칸, (2,2) = 네 칸.")]
        [SerializeField] private Vector2Int _size = Vector2Int.one;

        public Vector2Int Size => new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));

        /// <summary>왼쪽 위 기준 칸 (가장 작은 col, 가장 작은 row).</summary>
        public int AnchorCol { get; private set; }
        public int AnchorRow { get; private set; }

        protected MapGenerator Map { get; private set; }

        /// <summary>풀 반납 시점 (MapGenerator 내부에서 구독).</summary>
        public event Action<GridEntity> Recycled;

        public void Place(MapGenerator map, int anchorCol, int anchorRow, Vector3 worldCenter)
        {
            Map = map;
            AnchorCol = anchorCol;
            AnchorRow = anchorRow;
            transform.position = worldCenter;
            gameObject.SetActive(true);
            OnPlaced();
        }

        public void Recycle()
        {
            OnRecycled();
            gameObject.SetActive(false);
            Recycled?.Invoke(this);
        }

        public bool CoversCell(int col, int row)
        {
            Vector2Int s = Size;
            return col >= AnchorCol && col < AnchorCol + s.x &&
                   row >= AnchorRow && row < AnchorRow + s.y;
        }

        // IDamageable
        public bool TakeDamage(int amount) => ApplyDamage(amount);

        /// <summary>피해 적용. 파괴되면 true 를 반환하고, 내부에서 <see cref="MapGenerator.ClearEntity"/> 를 호출한다.</summary>
        public abstract bool ApplyDamage(int amount);

        protected virtual void OnPlaced() { }
        protected virtual void OnRecycled() { }
    }
}
