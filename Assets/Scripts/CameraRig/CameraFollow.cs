using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// 대상을 따라가되 아래쪽으로 치우쳐(플레이어가 화면 상단에 오도록) 하강이 잘 보이게 한다.
    /// 맵이 x=0 중앙 정렬이므로 x 는 고정, y 만 부드럽게 추적.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("대상 기준 오프셋. y 를 음수로 두면 대상이 화면 위쪽에 위치.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, -2.5f, -10f);

        [Tooltip("y 추적 부드러움(작을수록 빠름).")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Tooltip("x 도 대상을 따라갈지. 맵이 중앙 정렬이면 꺼둔다.")]
        [SerializeField] private bool _followX = false;

        [Tooltip("한 번 내려간 높이 위로는 다시 올라가지 않음(카메라가 되돌아가지 않게).")]
        [SerializeField] private bool _descendOnly = true;

        private float _yVelocity;
        private float _lowestY = float.PositiveInfinity;

        private void Start()
        {
            if (_target != null)
            {
                Vector3 p = _target.position + _offset;
                _lowestY = p.y;
                transform.position = new Vector3(_followX ? p.x : transform.position.x, p.y, _offset.z);
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 desired = _target.position + _offset;

            float targetY = desired.y;
            if (_descendOnly)
            {
                _lowestY = Mathf.Min(_lowestY, targetY);
                targetY = _lowestY;
            }

            float y = Mathf.SmoothDamp(transform.position.y, targetY, ref _yVelocity, _smoothTime);
            float x = _followX ? desired.x : transform.position.x;

            transform.position = new Vector3(x, y, _offset.z);
        }
    }
}
