using System;
using UnityEngine;
using Game.Flow;

namespace Game.Economy
{
    /// <summary>
    /// 이번 런에서 모은 재화 2종. 씬 리로드마다 0 에서 시작.
    /// 사망 시 <see cref="GameManager.EndRun"/> 에서 <see cref="BankToTotal"/> 로 영구 재화에 적립.
    /// 항상 활성인 오브젝트에 붙일 것.
    /// </summary>
    public class RunWallet : MonoBehaviour
    {
        public static RunWallet Instance { get; private set; }

        private readonly int[] _amounts = new int[CurrencyTypeExtensions.Count];

        /// <summary>재화가 변했을 때 (HUD 갱신용).</summary>
        public event Action Changed;

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

        public int Get(CurrencyType type) => _amounts[(int)type];

        public void Add(CurrencyType type, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            _amounts[(int)type] += amount;
            Changed?.Invoke();
        }

        /// <summary>이번 런 재화를 영구 누적에 합산.</summary>
        public void BankToTotal()
        {
            for (int i = 0; i < _amounts.Length; i++)
            {
                if (_amounts[i] > 0)
                {
                    GameManager.AddTotalCurrency((CurrencyType)i, _amounts[i]);
                }
            }
        }
    }
}
