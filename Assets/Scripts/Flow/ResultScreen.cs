using TMPro;
using UnityEngine;
using Game.UI;

namespace Game.Flow
{
    /// <summary>
    /// 사망 되감기가 끝나면 표시되는 결과 표시(순수 뷰).
    /// 표시 유지 시간 → 페이드 → 씬 리로드 흐름은 <see cref="GameManager"/> 가 제어.
    ///
    /// 배치: 결과 패널(ResultPanel) 오브젝트에 붙인다. 이 오브젝트는 **항상 활성**이며,
    /// 보이기/숨기기는 CanvasGroup.alpha 로 처리한다 (SetActive 토글이 아님).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _bestText;
        [Tooltip("이번 런에서 얻은 재화. CurrencyDisplay(Source=Run) 하나 붙이고 연결.")]
        [SerializeField] private CurrencyDisplay _rewardDisplay;
        [Tooltip("신기록일 때만 켜지는 오브젝트. 선택.")]
        [SerializeField] private GameObject _newBestBadge;

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
            SetVisible(false);
            if (_newBestBadge != null)
            {
                _newBestBadge.SetActive(false);
            }
        }

        public void Show(int depth, int bestDepth, bool newBest)
        {
            SetVisible(true);

            if (_scoreText != null)
            {
                _scoreText.text = $"Score\n{depth} m";
            }
            if (_bestText != null)
            {
                _bestText.text = $"Best Score\n{bestDepth} m";
            }
            if (_newBestBadge != null)
            {
                _newBestBadge.SetActive(newBest);
            }

            if (_rewardDisplay != null)
            {
                _rewardDisplay.Refresh();
            }
        }

        public void Hide() => SetVisible(false);

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
