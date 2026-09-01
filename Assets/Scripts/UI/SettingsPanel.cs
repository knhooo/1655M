using UnityEngine;
using UnityEngine.UI;
using Game.Audio;

namespace Game.UI
{
    /// <summary>
    /// 사운드 설정 패널. BGM/SFX 볼륨 슬라이더 + 음소거 토글.
    /// 타이틀의 "설정" 버튼 onClick 에서 <see cref="Show"/> / <see cref="Hide"/> / <see cref="Toggle"/> 연결.
    /// 값은 <see cref="BgmPlayer"/> / <see cref="SfxPlayer"/> 에 반영되고 PlayerPrefs 에 저장된다.
    ///
    /// 배치: 별도 패널 오브젝트에 이 컴포넌트를 붙인다. GameObject 를 켜두든 꺼두든
    /// 버튼에서 Toggle() 을 부르면 열린다 (GameObject.SetActive 로 표시/숨김).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Tooltip("선택 — 페이드가 필요하면. 없으면 GameObject 만 켜고 끈다.")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("실제 UI 를 담은 자식. 비우면 이 GameObject 자체를 켜고 끈다.")]
        [SerializeField] private GameObject _content;
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Toggle _bgmMuteToggle;
        [SerializeField] private Toggle _sfxMuteToggle;
        [SerializeField] private Button _closeButton;

        private bool _hooked;
        private bool _initializing;

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
                // 열려 있을 때 뒤 요소로 클릭이 새지 않도록 CanvasGroup 을 보장 (blocksRaycasts)
                _group = _content.GetComponent<CanvasGroup>();
                if (_group == null) _group = _content.AddComponent<CanvasGroup>();
            }

            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmSlider);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxSlider);
            if (_bgmMuteToggle != null) _bgmMuteToggle.onValueChanged.AddListener(OnBgmMute);
            if (_sfxMuteToggle != null) _sfxMuteToggle.onValueChanged.AddListener(OnSfxMute);
        }

        public void Show()
        {
            Hook();
            RefreshFromCurrent();
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
            if (IsShown())
            {
                SetShown(false);
            }
            else
            {
                RefreshFromCurrent();
                SetShown(true);
            }
        }

        private void RefreshFromCurrent()
        {
            _initializing = true;
            if (_bgmSlider != null) _bgmSlider.value = BgmPlayer.Volume;
            if (_sfxSlider != null) _sfxSlider.value = SfxPlayer.Volume;
            if (_bgmMuteToggle != null) _bgmMuteToggle.isOn = BgmPlayer.Muted;
            if (_sfxMuteToggle != null) _sfxMuteToggle.isOn = SfxPlayer.Muted;
            _initializing = false;
        }

        private void OnBgmSlider(float v) { if (!_initializing) BgmPlayer.SetVolume(v); }
        private void OnSfxSlider(float v) { if (!_initializing) SfxPlayer.SetVolume(v); }
        private void OnBgmMute(bool m) { if (!_initializing) BgmPlayer.SetMuted(m); }
        private void OnSfxMute(bool m) { if (!_initializing) SfxPlayer.SetMuted(m); }

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
