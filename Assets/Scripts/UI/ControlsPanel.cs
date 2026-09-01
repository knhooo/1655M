using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 조작법 안내 패널. 타이틀의 "조작법" 버튼 onClick 에서 <see cref="Show"/> / <see cref="Hide"/> / <see cref="Toggle"/> 연결.
    /// 내용(TMP_Text)은 인스펙터에서 직접 채운다.
    ///
    /// 배치: 별도 패널 오브젝트에 이 컴포넌트를 붙인다. GameObject 를 켜두든 꺼두든 상관없이
    /// 버튼에서 Toggle() 을 부르면 열린다 (GameObject.SetActive 로 표시/숨김).
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        [Tooltip("선택 — 열려 있을 때 반투명 페이드가 필요하면. 없으면 GameObject 만 켜고 끈다.")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _closeButton;
        [SerializeField] private GameObject _content;

        private bool _hooked;

        private void Awake()
        {
            Hook();
            SetShown(false);
        }

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            if (_content == null) _content = _group != null ? _group.gameObject : gameObject;
            if (_group == null)
            {
                _group = _content.GetComponent<CanvasGroup>();
                if (_group == null) _group = _content.AddComponent<CanvasGroup>();
            }
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            Hook();
            SetShown(true);
        }

        public void Hide()
        {
            Hook();
            SetShown(false);
        }

        public void Toggle()
        {
            Hook();
            SetShown(!IsShown());
        }

        private bool IsShown()
        {
            if (_content != null && !_content.activeSelf) return false;
            return _group == null || _group.alpha > 0.5f;
        }

        private void SetShown(bool visible)
        {
            if (_content != null)
            {
                _content.SetActive(visible);
            }
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = visible;
                _group.interactable = visible;
            }
        }
    }
}
