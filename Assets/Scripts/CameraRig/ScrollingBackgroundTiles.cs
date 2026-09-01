using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// 무한 스크롤 배경 (스프라이트 재활용 방식 — 머티리얼/셰이더 불필요).
    /// 같은 배경 스프라이트 2~3장을 세로로 쌓아 두면, 카메라가 내려가면서 화면 위로 벗어난 조각을
    /// 스택 맨 아래로 순환시킨다.
    ///
    /// 세팅:
    ///  1. 빈 오브젝트("Background") 밑에 배경 SpriteRenderer 를 <b>2~3장</b> 자식으로 둔다 (같은 스프라이트).
    ///     스프라이트는 세로로 이어 붙였을 때 자연스럽게 반복되는 것.
    ///  2. 이 컴포넌트를 부모에 붙이고 _followTarget(메인 카메라) 연결. _tiles 는 비우면 자식에서 자동.
    ///  3. Z 를 뒤로, sorting layer/order 를 맨 뒤로.
    /// </summary>
    public class ScrollingBackgroundTiles : MonoBehaviour
    {
        [SerializeField] private Transform _followTarget;
        [Tooltip("배경 조각들. 비우면 자식 SpriteRenderer 자동 수집.")]
        [SerializeField] private SpriteRenderer[] _tiles;
        [Tooltip("조각 한 장의 세로 높이(월드). 0 이면 스프라이트 크기에서 자동.")]
        [SerializeField] private float _tileHeight = 0f;
        [Tooltip("카메라 좌우 이동을 따라갈지.")]
        [SerializeField] private bool _followX = true;

        private float _h;
        private float _total;

        private void Start()
        {
            if (_tiles == null || _tiles.Length == 0)
            {
                _tiles = GetComponentsInChildren<SpriteRenderer>();
            }
            if (_tiles.Length == 0)
            {
                enabled = false;
                return;
            }

            _h = _tileHeight > 0f ? _tileHeight : GuessTileHeight();
            _total = _h * _tiles.Length;

            // 카메라 기준으로 세로로 정렬 (한 장은 위, 나머지는 아래로)
            float camY = _followTarget != null ? _followTarget.position.y : 0f;
            float camX = _followTarget != null ? _followTarget.position.x : 0f;
            for (int i = 0; i < _tiles.Length; i++)
            {
                Vector3 p = _tiles[i].transform.position;
                if (_followX) p.x = camX;
                p.y = camY + _h * (1 - i); // i=0 위, i=1 중앙, i=2 아래 ...
                _tiles[i].transform.position = p;
            }
        }

        private float GuessTileHeight()
        {
            SpriteRenderer sr = _tiles[0];
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                return sr.size.y * Mathf.Abs(sr.transform.lossyScale.y);
            }
            return sr.sprite != null
                ? sr.sprite.bounds.size.y * Mathf.Abs(sr.transform.lossyScale.y)
                : 5f;
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
            {
                return;
            }
            float camX = _followTarget.position.x;
            float camY = _followTarget.position.y;

            foreach (SpriteRenderer t in _tiles)
            {
                Vector3 p = t.transform.position;
                if (_followX)
                {
                    p.x = camX;
                }
                // 화면 위로 완전히 벗어났으면 스택 맨 아래로 순환
                while (p.y - camY > _h)
                {
                    p.y -= _total;
                }
                // (안전) 아래로 너무 멀면 위로
                while (camY - p.y > _total - _h)
                {
                    p.y += _total;
                }
                t.transform.position = p;
            }
        }
    }
}
