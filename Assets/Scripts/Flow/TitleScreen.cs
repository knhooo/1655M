using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Flow
{
    /// <summary>
    /// 타이틀 화면: 게임 제목 / Best Score / Play 버튼.
    /// Play 버튼은 맥락 인식 — 1번째 누르면 인벤토리, 2번째 누르면 하강 시작.
    /// TitlePanel 오브젝트에 붙인다. 표시/숨김은 CanvasGroup.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _bestScoreText;
        [SerializeField] private Button _playButton;

        [Header("Play 버튼 라벨")]
        [SerializeField] private TMP_Text _playButtonLabel;
        [Tooltip("타이틀 상태 (다음 누르면 인벤토리 열림).")]
        [SerializeField] private string _titleLabel = "Ready";
        [Tooltip("인벤토리 상태 (다음 누르면 하강 시작).")]
        [SerializeField] private string _inventoryLabel = "Play";

        private void Reset()
        {
            _group = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlay);
            }
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(OnPlay);
            }
        }

        public void Show()
        {
            SetVisible(true);
            RefreshBest();
            SetReadyMode();
        }

        public void Hide() => SetVisible(false);

        /// <summary>Play 버튼 라벨을 "Ready" 로 (다음 누르면 인벤토리).</summary>
        public void SetReadyMode() => SetPlayLabel(_titleLabel);

        /// <summary>Play 버튼 라벨을 "Play" 로 (다음 누르면 하강 시작).</summary>
        public void SetPlayMode() => SetPlayLabel(_inventoryLabel);

        private void SetPlayLabel(string text)
        {
            if (_playButtonLabel != null)
            {
                _playButtonLabel.text = text;
            }
        }

        private void RefreshBest()
        {
            if (_bestScoreText != null)
            {
                _bestScoreText.text = $"{GameManager.BestDepth} m";
            }
        }

        private void OnPlay()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayPressed();
            }
        }

        private void SetVisible(bool visible)
        {
            if (_group == null)
            {
                return;
            }
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
    }
}
