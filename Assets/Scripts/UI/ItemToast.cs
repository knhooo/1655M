using System.Collections;
using TMPro;
using UnityEngine;
using Game.Interactables;

namespace Game.UI
{
    /// <summary>
    /// 아이템을 획득하면 화면 상단에 라벨을 잠깐 띄웠다가 사라지게 한다.
    /// <see cref="PickupEntity.Picked"/>(static) 를 구독한다.
    ///
    /// 배치: 화면 상단 UI 패널에 붙이고 _group(CanvasGroup) · _label(TMP_Text) 연결. 기본 alpha 0.
    /// _panel 을 연결하면 위에서 살짝 내려오며 등장한다.
    /// </summary>
    public class ItemToast : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _label;
        [Tooltip("선택. 위에서 슬라이드 인 시킬 패널 RectTransform.")]
        [SerializeField] private RectTransform _panel;

        [SerializeField] private float _fadeIn = 0.15f;
        [SerializeField] private float _hold = 1.4f;
        [SerializeField] private float _fadeOut = 0.4f;
        [Tooltip("등장 시 이만큼 위(px)에서 제자리로 내려온다.")]
        [SerializeField] private float _slideFrom = 40f;

        private Coroutine _co;
        private Vector2 _panelHome;

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
            if (_panel != null)
            {
                _panelHome = _panel.anchoredPosition;
            }
            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        private void OnEnable() => PickupEntity.Picked += Show;
        private void OnDisable() => PickupEntity.Picked -= Show;

        private void Show(string text)
        {
            if (_group == null)
            {
                return;
            }
            if (_label != null)
            {
                _label.text = text;
            }
            if (_co != null)
            {
                StopCoroutine(_co);
            }
            _co = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            float din = Mathf.Max(0.01f, _fadeIn);
            float t = 0f;
            while (t < din)
            {
                t += Time.unscaledDeltaTime;
                float k = t / din;
                _group.alpha = k;
                if (_panel != null)
                {
                    _panel.anchoredPosition = _panelHome + Vector2.up * Mathf.Lerp(_slideFrom, 0f, k);
                }
                yield return null;
            }
            _group.alpha = 1f;
            if (_panel != null)
            {
                _panel.anchoredPosition = _panelHome;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _hold));

            float dout = Mathf.Max(0.01f, _fadeOut);
            t = 0f;
            while (t < dout)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - t / dout;
                yield return null;
            }
            _group.alpha = 0f;
            _co = null;
        }
    }
}
