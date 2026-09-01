using UnityEngine;
using UnityEngine.UI;
using Game.Audio;

namespace Game.UI
{
    /// <summary>
    /// 사운드 설정 패널. BGM/SFX 볼륨 슬라이더 + 음소거 토글.
    /// 타이틀의 "설정" 버튼 onClick 에서 <see cref="Show"/> 또는 <see cref="Toggle"/> 을 연결.
    /// 값은 <see cref="BgmPlayer"/> / <see cref="SfxPlayer"/> 에 그대로 반영되고 PlayerPrefs 에 저장된다.
    /// 배치: CanvasGroup 있는 패널에. 기본 숨김.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _sfxSlider;
        [Tooltip("선택. 켜면 해당 음소거.")]
        [SerializeField] private Toggle _bgmMuteToggle;
        [SerializeField] private Toggle _sfxMuteToggle;
        [SerializeField] private Button _closeButton;

        private bool _initializing;

        private void Awake()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmSlider);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxSlider);
            if (_bgmMuteToggle != null) _bgmMuteToggle.onValueChanged.AddListener(OnBgmMute);
            if (_sfxMuteToggle != null) _sfxMuteToggle.onValueChanged.AddListener(OnSfxMute);
            SetVisible(false);
        }

        public void Show()
        {
            RefreshFromCurrent();
            SetVisible(true);
        }

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

        private void SetVisible(bool visible)
        {
            if (_group == null) return;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
    }
}
