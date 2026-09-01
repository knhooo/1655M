using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.UI;

namespace Game.Flow
{
    /// <summary>
    /// 위(화면 밖)에서 내려오는 인벤토리 패널.
    ///  - 제목 / 재화 개수 / 탭 3개(Status · Sword · Items) + 각 페이지
    ///  - 시작(하강)은 타이틀 Play 버튼을 다시 눌러서 → 여기엔 시작 버튼 없음
    ///
    /// 배치: 인벤토리 패널(RectTransform)에 붙인다. **항상 활성**이며 위치로 숨긴다.
    /// 디자이너가 "보이는 위치"에 배치해 두면 Awake 가 기억하고 시작 시 화면 위로 올린다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryUI : MonoBehaviour
    {
        [Serializable]
        private class Tab
        {
            public Button button;
            public GameObject page;
        }

        [SerializeField] private RectTransform _rt;

        [Header("내용")]
        [Tooltip("Status / Sword / Items 순. button 과 page 를 짝지어 연결.")]
        [SerializeField] private Tab[] _tabs;
        [Tooltip("타이틀로 돌아가기. 선택.")]
        [SerializeField] private Button _backButton;
        [Tooltip("재화 라벨 묶음. 패널 상단에 CurrencyDisplay(Source=Total) 하나 붙이고 연결.")]
        [SerializeField] private CurrencyDisplay _currencyDisplay;

        [Header("Slide")]
        [Tooltip("숨길 때 위로 이동시킬 거리(px). 0 이면 패널 높이 + 200 자동.")]
        [SerializeField] private float _hiddenOffsetY = 1400f;
        [Tooltip("보일 때 씬에 배치한 위치보다 이만큼(px) 더 아래로 내린다. 양수 = 더 내려옴.")]
        [SerializeField] private float _shownExtraDropY = 0f;
        [SerializeField] private float _slideDuration = 0.35f;

        private Vector2 _shownPos;
        private Vector2 _hiddenPos;
        private Coroutine _slide;

        private void Reset()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (_rt == null)
            {
                _rt = GetComponent<RectTransform>();
            }

            _shownPos = _rt.anchoredPosition + Vector2.down * _shownExtraDropY;
            float offset = _hiddenOffsetY > 0f ? _hiddenOffsetY : _rt.rect.height + 200f;
            _hiddenPos = _shownPos + Vector2.up * offset;

            if (_tabs != null)
            {
                for (int i = 0; i < _tabs.Length; i++)
                {
                    int index = i;
                    if (_tabs[i] != null && _tabs[i].button != null)
                    {
                        _tabs[i].button.onClick.AddListener(() => SelectTab(index));
                    }
                }
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(OnBack);
            }

            CloseAllTabs(); // 시작 시 모든 페이지 닫힘
        }

        public void HideInstant()
        {
            StopSlide();
            _rt.anchoredPosition = _hiddenPos;
        }

        public void SlideIn()
        {
            CloseAllTabs(); // 열 때는 아무 탭도 선택 안 됨 - 버튼을 눌러야 페이지가 뜸
            if (_currencyDisplay != null)
            {
                _currencyDisplay.Refresh(); // 뱅킹 직후 등 이벤트로 못 잡는 변화 반영
            }
            StartSlide(_shownPos);
        }

        public void SlideOut() => StartSlide(_hiddenPos);

        /// <summary>index 페이지만 활성, 나머지 비활성. index 가 범위 밖이면 전부 비활성.</summary>
        private void SelectTab(int index)
        {
            if (_tabs == null)
            {
                return;
            }
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (_tabs[i] != null && _tabs[i].page != null)
                {
                    _tabs[i].page.SetActive(i == index);
                }
            }
        }

        private void CloseAllTabs() => SelectTab(-1);

        // ------------------------------------------------------------------
        // 슬라이드
        // ------------------------------------------------------------------

        private void StartSlide(Vector2 target)
        {
            StopSlide();
            if (isActiveAndEnabled)
            {
                _slide = StartCoroutine(SlideRoutine(target));
            }
            else
            {
                _rt.anchoredPosition = target;
            }
        }

        private void StopSlide()
        {
            if (_slide != null)
            {
                StopCoroutine(_slide);
                _slide = null;
            }
        }

        private IEnumerator SlideRoutine(Vector2 target)
        {
            Vector2 from = _rt.anchoredPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _slideDuration);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                _rt.anchoredPosition = Vector2.LerpUnclamped(from, target, k);
                yield return null;
            }
            _rt.anchoredPosition = target;
            _slide = null;
        }

        private void OnBack()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CloseInventory();
            }
        }
    }
}
