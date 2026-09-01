using UnityEngine;
using Game.Player;

namespace Game.Interactables
{
    /// <summary>
    /// ㄹ자 대시 아이템. 획득 즉시 지정된 방향열을 따라 연속 돌진하며 지층·적을 뚫는다.
    /// <see cref="_pattern"/> 은 단위 스텝: (1,0) 우 · (-1,0) 좌 · (0,1) 아래. (위는 불가)
    /// 기본값 = 우3 → 아래1 → 좌3 → 아래1 → 우3 (ㄹ 모양).
    /// </summary>
    public class DashItem : PickupEntity
    {
        [Header("ㄹ자 대시")]
        [Tooltip("단위 스텝 방향열. (1,0)우 (-1,0)좌 (0,1)아래.")]
        [SerializeField] private Vector2Int[] _pattern =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(1, 0),
        };
        [SerializeField] private float _damageMultiplier = 2f;
        [SerializeField] private float _stepDuration = 0.045f;

        protected override string OnCollected(PlayerController pc)
        {
            pc.StartPathDash(_pattern, _damageMultiplier, _stepDuration);
            return "ㄹ DASH!";
        }
    }
}
