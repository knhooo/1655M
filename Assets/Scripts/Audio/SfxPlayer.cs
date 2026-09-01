using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 효과음 재생 싱글톤. 어디서든 <c>SfxPlayer.Play(SfxId.Dig)</c> 로 호출.
    /// 여러 AudioSource 를 라운드로빈으로 돌려 겹쳐 재생. 클립은 <see cref="_bank"/> 에서 가져온다.
    ///
    /// 배치: 항상 활성인 오브젝트에 하나. _bank 에 SfxBank 애셋 연결.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [SerializeField] private SfxBank _bank;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.8f;
        [SerializeField] private bool _muted;
        [Tooltip("동시 재생 채널 수.")]
        [SerializeField, Range(2, 24)] private int _voices = 12;
        [Tooltip("같은 사운드가 이 시간(초) 안에 또 오면 무시 — 기관총 방지.")]
        [SerializeField] private float _minRetrigger = 0.035f;

        private AudioSource[] _sources;
        private int _next;
        private readonly Dictionary<SfxId, float> _lastPlayed = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _sources = new AudioSource[Mathf.Max(2, _voices)];
            for (int i = 0; i < _sources.Length; i++)
            {
                var go = new GameObject("voice_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D 기본
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 30f;
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

        public static void Play(SfxId id) => Instance?.PlayInternal(id, null);
        public static void PlayAt(SfxId id, Vector3 worldPos) => Instance?.PlayInternal(id, worldPos);

        // ---- 내부 ----

        private void PlayInternal(SfxId id, Vector3? pos)
        {
            if (_muted || _bank == null || id == SfxId.None)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out float last) && now - last < _minRetrigger)
            {
                return;
            }
            _lastPlayed[id] = now;

            if (!_bank.TryGet(id, out AudioClip clip, out float vol, out float pitch) || clip == null)
            {
                return;
            }

            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Length;

            src.clip = clip;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(vol * _masterVolume);
            if (pos.HasValue)
            {
                src.spatialBlend = 1f;
                src.transform.position = pos.Value;
            }
            else
            {
                src.spatialBlend = 0f;
            }
            src.Play();
        }
    }
}
