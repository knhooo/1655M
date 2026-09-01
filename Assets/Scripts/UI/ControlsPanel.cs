using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 조작법 안내 패널. 타이틀의 "조작법" 버튼 onClick 에서 <see cref="Show"/> 또는 <see cref="Toggle"/> 을 연결.
    /// 내용(TMP_Text)은 인스펙터에서 직접 채운다 — 고정 텍스트라 별도 스크립트 로직 없음.
    /// 배치: CanvasGroup 있는 패널에. 기본 숨김.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ControlsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
            SetVisible(false);
        }

        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);

        public void Toggle()
        {
            if (_group != null && _group.alpha > 0.5f)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        private void SetVisible(bool visible)
        {
            if (_group == null) return;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
    }
}
