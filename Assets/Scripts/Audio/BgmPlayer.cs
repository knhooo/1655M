using System.Collections;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 배경음악 재생 싱글톤. 타이틀/인게임 트랙을 크로스페이드로 전환한다.
    /// 볼륨·음소거는 PlayerPrefs 에 저장되어 씬을 넘어 유지된다.
    ///
    /// 배치: SfxPlayer 옆(항상 활성인 오브젝트)에 하나. <see cref="_titleClip"/> / <see cref="_gameplayClip"/> 연결.
    /// 호출: <see cref="PlayTitle"/> / <see cref="PlayGameplay"/> — <c>GameManager</c> 상태 전이에서.
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        [SerializeField] private AudioClip _titleClip;
        [SerializeField] private AudioClip _gameplayClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.55f;
        [SerializeField] private bool _muted;
        [SerializeField] private float _fadeTime = 1f;

        private const string VolumeKey = "bgm_volume";
        private const string MutedKey = "bgm_muted";

        private AudioSource[] _sources; // 크로스페이드용 2개
        private int _current;
        private AudioClip _pendingClip; // 지금 재생/페이드 중인 트랙 (중복 재생 방지)
        private Coroutine _fadeCo;

        public static float Volume => Instance != null ? Instance._volume : 0.55f;
        public static bool Muted => Instance != null && Instance._muted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _volume = PlayerPrefs.GetFloat(VolumeKey, _volume);
            _muted = PlayerPrefs.GetInt(MutedKey, _muted ? 1 : 0) == 1;

            _sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("bgm_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                _sources[i] = src;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---- 정적 진입점 ----

        public static void PlayTitle()
        {
            if (Instance != null) Instance.CrossfadeTo(Instance._titleClip);
        }

        public static void PlayGameplay()
        {
            if (Instance != null) Instance.CrossfadeTo(Instance._gameplayClip);
        }

        public static void SetVolume(float v)
        {
            if (Instance == null) return;
            Instance._volume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(VolumeKey, Instance._volume);
            PlayerPrefs.Save();
            Instance.ApplyVolume();
        }

        public static void SetMuted(bool muted)
        {
            if (Instance == null) return;
            Instance._muted = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Instance.ApplyVolume();
        }

        // ---- 내부 ----

        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null || clip == _pendingClip)
            {
                return; // 같은 트랙이면 재시작하지 않음
            }
            _pendingClip = clip;

            if (_fadeCo != null)
            {
                StopCoroutine(_fadeCo);
            }
            _fadeCo = StartCoroutine(FadeRoutine(clip));
        }

        private IEnumerator FadeRoutine(AudioClip clip)
        {
            int next = 1 - _current;
            AudioSource from = _sources[_current];
            AudioSource to = _sources[next];

            to.clip = clip;
            to.volume = 0f;
            to.Play();

            float t = 0f;
            float dur = Mathf.Max(0.05f, _fadeTime);
            float fromStart = from.volume;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                // 목표 볼륨을 매 프레임 다시 읽음 — 페이드 도중 슬라이더를 움직여도 즉시 반영
                to.volume = Mathf.Lerp(0f, EffectiveVolume(), k);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                yield return null;
            }
            to.volume = EffectiveVolume();
            from.volume = 0f;
            from.Stop();

            _current = next;
            _fadeCo = null;
        }

        private void ApplyVolume()
        {
            float v = EffectiveVolume();
            if (_sources == null) return;
            // 현재 재생 중인 소스만 갱신 (페이드 중이면 다음 페이드가 알아서 target 을 반영)
            if (_fadeCo == null)
            {
                _sources[_current].volume = v;
            }
        }

        private float EffectiveVolume() => _muted ? 0f : _volume;
    }
}
