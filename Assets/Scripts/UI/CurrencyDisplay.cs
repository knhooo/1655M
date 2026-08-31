using System;
using TMPro;
using UnityEngine;
using Game.Player;
using Game.Economy;
using Game.Flow;

namespace Game.UI
{
    /// <summary>
    /// 여러 재화 라벨을 한 컴포넌트에서 관리한다. 인벤토리 헤더 / HUD / 결과 패널 등에 하나만 붙인다.
    ///  - Total : 아웃게임 누적(GameManager). 강화 시 자동 갱신.
    ///  - Run   : 이번 런에서 모은 양(RunWallet). 획득 시 자동 갱신.
    /// 결과 패널처럼 이벤트 밖에서 갱신이 필요하면 <see cref="Refresh"/> 를 직접 호출.
    /// </summary>
    public class CurrencyDisplay : MonoBehaviour
    {
        public enum Source { Total, Run }

        [Serializable]
        private class Entry
        {
            public CurrencyType type;
            public TMP_Text text;
            [Tooltip("{0} 자리에 값. 예: \"{0}\", \"Coin {0}\", \"+{0}\".")]
            public string format = "{0}";
        }

        [SerializeField] private Source _source = Source.Total;
        [SerializeField] private Entry[] _entries;

        private void OnEnable()
        {
            if (_source == Source.Total)
            {
                PlayerStats.Changed += Refresh;
            }
            else if (RunWallet.Instance != null)
            {
                RunWallet.Instance.Changed += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_source == Source.Total)
            {
                PlayerStats.Changed -= Refresh;
            }
            else if (RunWallet.Instance != null)
            {
                RunWallet.Instance.Changed -= Refresh;
            }
        }

        /// <summary>모든 라벨 갱신. 외부에서 강제 호출 가능.</summary>
        public void Refresh()
        {
            if (_entries == null)
            {
                return;
            }
            RunWallet wallet = RunWallet.Instance;
            foreach (Entry e in _entries)
            {
                if (e == null || e.text == null)
                {
                    continue;
                }
                int value = _source == Source.Total
                    ? GameManager.GetTotalCurrency(e.type)
                    : (wallet != null ? wallet.Get(e.type) : 0);
                string fmt = string.IsNullOrEmpty(e.format) ? "{0}" : e.format;
                e.text.text = string.Format(fmt, value);
            }
        }
    }
}
