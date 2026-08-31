using System;
using UnityEngine;

namespace Game.Equipment
{
    /// <summary>
    /// 등급별 검 스프라이트 모음. 애셋 하나로 만들어 Sword 탭 · 인게임 무기 · 상자 보상 등에서 공유한다.
    /// 생성: Project 창 우클릭 → Create → 1655M → Sword Icon Set.
    /// </summary>
    [CreateAssetMenu(menuName = "1655M/Sword Icon Set", fileName = "SwordIconSet")]
    public class SwordIconSet : ScriptableObject
    {
        [Serializable]
        private class Entry
        {
            public SwordRarity rarity;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] _entries;
        [Tooltip("해당 등급 스프라이트가 없을 때 사용. 선택.")]
        [SerializeField] private Sprite _fallback;

        /// <summary>등급에 해당하는 스프라이트. 없으면 fallback(또는 null).</summary>
        public Sprite Get(SwordRarity rarity)
        {
            if (_entries != null)
            {
                foreach (Entry e in _entries)
                {
                    if (e != null && e.rarity == rarity && e.sprite != null)
                    {
                        return e.sprite;
                    }
                }
            }
            return _fallback;
        }
    }
}
