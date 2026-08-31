using System;
using UnityEngine;

namespace Game.Equipment
{
    /// <summary>
    /// 등급별 검 그래픽 모음. 애셋 하나로 만들어 Sword 탭 · 인게임 무기 · 상자 보상 등에서 공유한다.
    ///  - icon   : UI 아이콘 / 인게임 장착 무기 스프라이트
    ///  - effect : 공격 시 휘두르는 슬래시 이펙트 스프라이트
    /// 생성: Project 창 우클릭 → Create → 1655M → Sword Icon Set.
    /// </summary>
    [CreateAssetMenu(menuName = "1655M/Sword Icon Set", fileName = "SwordIconSet")]
    public class SwordIconSet : ScriptableObject
    {
        [Serializable]
        private class Entry
        {
            public SwordRarity rarity;
            [Tooltip("아이콘 / 장착 무기 스프라이트.")]
            public Sprite sprite;
            [Tooltip("공격 슬래시 이펙트 스프라이트.")]
            public Sprite effect;
            [Tooltip("인게임 무기 크기 배수. 0/미설정 = 1.")]
            public float spriteScale = 1f;
            [Tooltip("슬래시 이펙트 크기 배수. 0/미설정 = 1.")]
            public float effectScale = 1f;
        }

        [SerializeField] private Entry[] _entries;
        [Tooltip("해당 등급 아이콘이 없을 때 사용. 선택.")]
        [SerializeField] private Sprite _fallback;
        [Tooltip("해당 등급 이펙트가 없을 때 사용. 선택.")]
        [SerializeField] private Sprite _fallbackEffect;

        /// <summary>등급 아이콘. 없으면 fallback(또는 null).</summary>
        public Sprite Get(SwordRarity rarity)
        {
            Entry e = Find(rarity);
            return e != null && e.sprite != null ? e.sprite : _fallback;
        }

        /// <summary>등급 슬래시 이펙트. 없으면 fallbackEffect(또는 null).</summary>
        public Sprite GetEffect(SwordRarity rarity)
        {
            Entry e = Find(rarity);
            return e != null && e.effect != null ? e.effect : _fallbackEffect;
        }

        /// <summary>인게임 무기 크기 배수. 미설정이면 1.</summary>
        public float GetSpriteScale(SwordRarity rarity)
        {
            Entry e = Find(rarity);
            return e != null && e.spriteScale > 0f ? e.spriteScale : 1f;
        }

        /// <summary>슬래시 이펙트 크기 배수. 미설정이면 1.</summary>
        public float GetEffectScale(SwordRarity rarity)
        {
            Entry e = Find(rarity);
            return e != null && e.effectScale > 0f ? e.effectScale : 1f;
        }

        private Entry Find(SwordRarity rarity)
        {
            if (_entries != null)
            {
                foreach (Entry e in _entries)
                {
                    if (e != null && e.rarity == rarity)
                    {
                        return e;
                    }
                }
            }
            return null;
        }
    }
}
