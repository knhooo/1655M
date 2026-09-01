using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 효과음 뱅크. <see cref="SfxId"/> → 클립 배열(랜덤 선택) + 볼륨 + 피치 흔들림.
    /// 생성: Project 창 우클릭 → Create → 1655M → Sfx Bank.
    /// </summary>
    [CreateAssetMenu(menuName = "1655M/Sfx Bank", fileName = "SfxBank")]
    public class SfxBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            [Tooltip("여러 개면 재생할 때마다 랜덤으로 하나.")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("피치 랜덤 폭(±). 0.06 = 0.94~1.06. 같은 소리 반복 시 지루함 방지.")]
            [Range(0f, 0.4f)] public float pitchVariance = 0.06f;
        }

        [SerializeField] private Entry[] _entries;

        private Dictionary<SfxId, Entry> _map;

        public bool TryGet(SfxId id, out AudioClip clip, out float volume, out float pitch)
        {
            clip = null;
            volume = 1f;
            pitch = 1f;

            if (_map == null)
            {
                Build();
            }
            if (!_map.TryGetValue(id, out Entry e) || e.clips == null || e.clips.Length == 0)
            {
                return false;
            }

            clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
            volume = e.volume;
            pitch = 1f + UnityEngine.Random.Range(-e.pitchVariance, e.pitchVariance);
            return clip != null;
        }

        private void Build()
        {
            _map = new Dictionary<SfxId, Entry>();
            if (_entries != null)
            {
                foreach (Entry e in _entries)
                {
                    if (e != null)
                    {
                        _map[e.id] = e;
                    }
                }
            }
        }
    }
}
