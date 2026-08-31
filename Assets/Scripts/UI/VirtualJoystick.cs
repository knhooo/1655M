using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 우측 하단 가상 조이스틱. 드래그하면 <see cref="Value"/>(-1~1) 가 갱신되고,
    /// 손을 떼면 0 으로 복귀. <see cref="PlayerController"/> 가 키보드 입력 대신 읽어간다.
    ///
    /// 배치: 조이스틱 배경 이미지에 붙인다. 배경 Image 는 Raycast Target 켤 것.
    /// 씬에 EventSystem + Canvas GraphicRaycaster 필요.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _handle;
        [Tooltip("핸들이 움직일 수 있는 최대 반경(px).")]
        [SerializeField] private float _radius = 90f;
        [Tooltip("이 크기 미만 입력은 0 으로 무시.")]
        [SerializeField, Range(0f, 0.9f)] private float _deadZone = 0.25f;

        /// <summary>정규화된 방향 입력(-1~1). 손을 떼면 (0,0).</summary>
        public Vector2 Value { get; private set; }

        /// <summary>현재 조이스틱을 누르고 있는가.</summary>
        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (_background == null)
            {
                _background = transform as RectTransform;
            }
            ResetHandle();
        }

        private void OnDisable()
        {
            IsActive = false;
            Value = Vector2.zero;
            ResetHandle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background, eventData.position, eventData.pressEventCamera, out Vector2 local);

            Vector2 clamped = Vector2.ClampMagnitude(local, _radius);
            if (_handle != null)
            {
                _handle.anchoredPosition = clamped;
            }

            Vector2 v = clamped / _radius;
            Value = v.magnitude < _deadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            Value = Vector2.zero;
            ResetHandle();
        }

        private void ResetHandle()
        {
            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
