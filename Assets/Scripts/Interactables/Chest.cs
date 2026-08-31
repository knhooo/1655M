using System;
using UnityEngine;
using Game.Map;
using Game.Economy;
using Game.Equipment;

namespace Game.Interactables
{
    /// <summary>
    /// 격자에 지층 대신 배치되는 상자. 부수면 재화 또는 검이 나온다. 접촉 피해 없음.
    /// 배치·풀링은 적과 동일한 <see cref="GridEntity"/> / <see cref="MapGenerator"/> 경로.
    /// 상자 배치는 결정적(해시)이지만, 내용물은 매번 랜덤이다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Chest : GridEntity
    {
        [Serializable]
        private class Drop
        {
            public bool isSword;
            [Tooltip("isSword 일 때 지급할 검 등급.")]
            public SwordRarity swordRarity = SwordRarity.Normal;
            [Tooltip("검이 아닐 때 지급할 재화량.")]
            public int goldAmount = 10;
            [Min(0f)] public float weight = 1f;
        }

        [Header("Chest")]
        [SerializeField] private int _maxHp = 40;
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("가능한 드롭 목록. 가중치로 하나 선택. 예: 재화 w3, Normal검 w2, Unique검 w1 ...")]
        [SerializeField] private Drop[] _drops;

        /// <summary>부순 결과: (무기 여부, 표시용 라벨). 토스트/사운드용.</summary>
        public event Action<bool, string> Opened;

        private int _hp;

        private void Reset()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        protected override void OnPlaced()
        {
            _hp = Mathf.Max(1, _maxHp);
            if (_renderer != null)
            {
                _renderer.color = new Color(0.72f, 0.52f, 0.26f);
            }
        }

        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _hp <= 0)
            {
                return false;
            }

            _hp -= amount;
            if (_hp > 0)
            {
                if (_renderer != null)
                {
                    _renderer.color = new Color(0.55f, 0.40f, 0.20f); // 금 간 상자
                }
                return false;
            }

            GiveReward();
            Map.ClearEntity(this);
            return true;
        }

        private void GiveReward()
        {
            Drop d = PickDrop();
            if (d == null)
            {
                Opened?.Invoke(false, "빈 상자");
                return;
            }

            if (d.isSword)
            {
                RunInventory.Instance?.AddFound(d.swordRarity);
                Opened?.Invoke(true, d.swordRarity.DisplayName());
                Debug.Log($"[Chest] 검 획득: {d.swordRarity.DisplayName()}", this);
            }
            else
            {
                int gold = Mathf.Max(1, d.goldAmount);
                RunWallet.Instance?.AddGold(gold);
                Opened?.Invoke(false, $"+{gold}");
                Debug.Log($"[Chest] 재화 +{gold}", this);
            }
            // TODO: 드롭 파티클 / 픽업 튐 연출 / 사운드
        }

        private Drop PickDrop()
        {
            if (_drops == null || _drops.Length == 0)
            {
                return null;
            }

            float total = 0f;
            foreach (Drop d in _drops)
            {
                if (d != null)
                {
                    total += Mathf.Max(0f, d.weight);
                }
            }
            if (total <= 0f)
            {
                return null;
            }

            float pick = UnityEngine.Random.value * total;
            float acc = 0f;
            foreach (Drop d in _drops)
            {
                if (d == null)
                {
                    continue;
                }
                acc += Mathf.Max(0f, d.weight);
                if (pick <= acc)
                {
                    return d;
                }
            }
            return _drops[_drops.Length - 1];
        }
    }
}
