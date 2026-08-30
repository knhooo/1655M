using System;
using System.Collections;
using UnityEngine;

namespace Game.Flow
{
    /// <summary>
    /// 전체화면 검정 오버레이 페이드.
    /// 씬 로드 직후 검정에서 시작 → <see cref="FadeIn"/> 으로 밝게,
    /// 씬 전환 직전 <see cref="FadeOut"/> 으로 검정.
    /// CanvasGroup 이 있는, 화면을 덮는 UI(검정 Image) 에 붙인다.
    ///
    /// 주의: 이 오브젝트는 항상 활성이어야 한다. 숨기려면 오브젝트를 끄지 말고
    /// CanvasGroup.alpha 를 0 으로 둘 것 (Awake 에서 자동 처리).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _fadeDuration = 0.5f;

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
            _group.alpha = 1f; // 로드 직후 검정으로 시작
            _group.blocksRaycasts = true;
        }

        public void FadeIn() => StartFade(0f, null);

        public void FadeOut(Action onComplete) => StartFade(1f, onComplete);

        private void StartFade(float target, Action onComplete)
        {
            EnsureGroup();

            // 오브젝트가 비활성이면 코루틴을 못 돌리므로 즉시 적용 (시퀀스가 멈추지 않게).
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[ScreenFader] 오브젝트가 비활성 - 페이드 없이 즉시 적용. FadeCanvas 를 켜 두세요.", this);
                if (_group != null) _group.alpha = target;
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(FadeRoutine(target, onComplete));
        }

        private void EnsureGroup()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
        }

        private IEnumerator FadeRoutine(float target, Action onComplete)
        {
            float start = _group.alpha;
            _group.blocksRaycasts = true;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _fadeDuration);
                _group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t));
                yield return null;
            }

            _group.alpha = target;
            _group.blocksRaycasts = target > 0.5f;
            onComplete?.Invoke();
        }
    }
}
