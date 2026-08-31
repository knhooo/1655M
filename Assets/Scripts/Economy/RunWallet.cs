using System;
using UnityEngine;
using Game.Flow;

namespace Game.Economy
{
    /// <summary>
    /// 이번 런에서 모은 재화. 씬 리로드마다 0 에서 시작.
    /// 사망 시 <see cref="GameManager.EndRun"/> 에서 <see cref="BankToTotal"/> 로 영구 재화에 적립된다.
    /// 항상 활성인 오브젝트에 붙일 것.
    /// </summary>
    public class RunWallet : MonoBehaviour
    {
        public static RunWallet Instance { get; private set; }

        public int Gold { get; private set; }
        public event Action<int> GoldChanged;

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

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            Gold += amount;
            GoldChanged?.Invoke(Gold);
        }

        /// <summary>이번 런 재화를 영구 누적(GameManager.TotalGold)에 합산.</summary>
        public void BankToTotal()
        {
            if (Gold > 0)
            {
                GameManager.TotalGold += Gold;
            }
        }
    }
}
