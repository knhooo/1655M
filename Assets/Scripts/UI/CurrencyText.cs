using TMPro;
using UnityEngine;
using Game.Player;
using Game.Economy;
using Game.Flow;

namespace Game.UI
{
    /// <summary>
    /// 재화 하나를 숫자로 표시하는 재사용 라벨. 재화 종류마다 하나씩 붙인다.
    ///  - Total : 아웃게임 누적(GameManager). 강화 시 갱신.
    ///  - Run   : 이번 런에서 모은 양(RunWallet). 획득 시 갱신.
    /// TMP_Text 가 있는 오브젝트에 붙이고 _type 만 지정 (_text 비우면 자기 오브젝트에서 찾음).
    /// </summary>
    public class CurrencyText : MonoBehaviour
    {
        public enum Source { Total, Run }

        [SerializeField] private CurrencyType _type = CurrencyType.Coin;
        [SerializeField] private Source _source = Source.Total;
        [SerializeField] private TMP_Text _text;
        [Tooltip("예: \"Coin {0}\". {0} 자리에 숫자. 비우면 숫자만.")]
        [SerializeField] private string _format = "{0}";

        private void Reset()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

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

        private void Refresh()
        {
            if (_text == null)
            {
                return;
            }
            int value = _source == Source.Total
                ? GameManager.GetTotalCurrency(_type)
                : (RunWallet.Instance != null ? RunWallet.Instance.Get(_type) : 0);
            _text.text = string.Format(_format, value);
        }
    }
}
