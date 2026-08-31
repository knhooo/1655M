using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Equipment
{
    /// <summary>
    /// 이번 런에서 상자로 얻은 검 목록. 사망 시 영구 보유 목록(<see cref="PlayerPrefs"/>)에 병합.
    /// 항상 활성인 오브젝트에 붙일 것.
    /// </summary>
    public class RunInventory : MonoBehaviour
    {
        public static RunInventory Instance { get; private set; }

        private const string OwnedKey = "owned_swords"; // 쉼표로 이은 SwordRarity 정수값

        private readonly List<SwordRarity> _found = new();

        public IReadOnlyList<SwordRarity> Found => _found;

        /// <summary>이번 런에서 검을 얻은 순간.</summary>
        public event Action<SwordRarity> SwordFound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void AddFound(SwordRarity rarity)
        {
            _found.Add(rarity);
            SwordFound?.Invoke(rarity);
        }

        /// <summary>이번 런에서 찾은 검을 영구 보유 목록에 합친다.</summary>
        public void BankToOwned()
        {
            if (_found.Count == 0)
            {
                return;
            }

            var owned = new HashSet<int>();
            foreach (SwordRarity r in GetOwnedSwords())
            {
                owned.Add((int)r);
            }
            foreach (SwordRarity r in _found)
            {
                owned.Add((int)r);
            }

            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
            PlayerPrefs.Save();
        }

        /// <summary>영구 보유 중인 검 등급 목록.</summary>
        public static SwordRarity[] GetOwnedSwords()
        {
            string raw = PlayerPrefs.GetString(OwnedKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return Array.Empty<SwordRarity>();
            }

            string[] parts = raw.Split(',');
            var list = new List<SwordRarity>(parts.Length);
            foreach (string p in parts)
            {
                if (int.TryParse(p, out int v) && Enum.IsDefined(typeof(SwordRarity), v))
                {
                    list.Add((SwordRarity)v);
                }
            }
            return list.ToArray();
        }
    }
}
