using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// 무한 스크롤 배경. 카메라를 따라다니면서 <b>텍스처 오프셋만 내려간 거리만큼 밀어</b> 계속 반복되는 것처럼 보이게 한다.
    /// 오브젝트를 새로 생성하지 않으므로 가볍다.
    ///
    /// 세팅:
    ///  1. Quad(GameObject → 3D Object → Quad) 를 크게 스케일해 카메라 앞을 덮게 배치, Z 를 뒤로.
    ///  2. 머티리얼 = URP/Unlit, 타일 가능한 텍스처(Import 의 Wrap Mode = Repeat), Tiling 을 4x4 등으로.
    ///  3. 이 컴포넌트를 붙이고 _renderer / _followTarget(메인 카메라) 연결.
    ///  4. 여러 겹(패럴랙스)은 레이어마다 이 컴포넌트를 두고 _scrollPerWorldY 를 다르게.
    /// </summary>
    public class ScrollingBackground : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Transform _followTarget;
        [Tooltip("월드에서 1유닛 내려갈 때 텍스처를 얼마나 밀지(UV). 작을수록 천천히. 아래로 흐르면 음수.")]
        [SerializeField] private float _scrollPerWorldY = -0.04f;
        [Tooltip("좌우 이동 시 텍스처 스크롤. 보통 0.")]
        [SerializeField] private float _scrollPerWorldX = 0f;
        [Tooltip("배경 판이 카메라 Y 를 따라가는 비율. 1 = 화면 고정, <1 = 살짝 뒤처짐(깊이감).")]
        [SerializeField, Range(0f, 1f)] private float _followLerp = 1f;

        private Material _mat;               // 인스턴스 (이 배경 전용)
        private Vector2 _baseOffset;
        private Vector3 _startOffset;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }
            if (_renderer != null)
            {
                _mat = _renderer.material; // 인스턴스화 — 공유 머티리얼 안 건드림
                _baseOffset = _mat.mainTextureOffset;
            }
            _startOffset = transform.position - (_followTarget != null ? _followTarget.position : Vector3.zero);
        }

        private void OnDestroy()
        {
            if (_mat != null)
            {
                Destroy(_mat);
            }
        }

        private void LateUpdate()
        {
            if (_followTarget == null || _mat == null)
            {
                return;
            }

            Vector3 tp = _followTarget.position;

            // 배경 판을 카메라에 맞춤 (Z 유지)
            Vector3 want = new Vector3(tp.x + _startOffset.x, tp.y + _startOffset.y, transform.position.z);
            transform.position = _followLerp >= 1f ? want : Vector3.Lerp(transform.position, want, _followLerp);

            // 텍스처 오프셋 = 이동 거리 * 계수
            _mat.mainTextureOffset = _baseOffset + new Vector2(tp.x * _scrollPerWorldX, tp.y * _scrollPerWorldY);
        }
    }
}
