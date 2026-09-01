using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Skills;

namespace Game.Audio
{
    /// <summary>
    /// 씬의 모든 UGUI <see cref="Button"/> 의 onClick 에 공용 클릭 효과음을 자동으로 붙인다.
    /// 버튼 프리팹을 하나하나 건드리지 않기 위한 전역 방식.
    ///
    /// - 활성화 시 1회 스캔.
    /// - 런타임에 버튼을 생성하는 쪽(예: <c>SwordPanel</c>)이 <see cref="Rescan"/> 을 호출한다.
    /// - 인게임 스킬 버튼(<see cref="SkillButtonUI"/>)은 자체 스킬 효과음이 있어 제외.
    ///
    /// 배치: SfxPlayer 옆(항상 활성인 오브젝트)에 하나.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UiClickSound : MonoBehaviour
    {
        [SerializeField] private SfxId _sfx = SfxId.UiClick;

        private static UiClickSound _instance;
        private readonly HashSet<Button> _hooked = new HashSet<Button>();

        /// <summary>런타임에 버튼을 새로 만든 쪽에서 호출. 전체 스캔은 무겁지 않게 새 버튼만 훅한다.</summary>
        public static void Rescan() => _instance?.Scan();

        private void OnEnable()
        {
            _instance = this;
            Scan();
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Scan()
        {
            Button[] all = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            SfxId id = _sfx;
            foreach (Button b in all)
            {
                if (b == null || !_hooked.Add(b))
                {
                    continue; // 이미 처리했거나 파괴됨
                }
                if (b.GetComponentInParent<SkillButtonUI>() != null)
                {
                    continue; // 스킬 버튼은 스킬 발동 효과음으로 대체
                }
                b.onClick.AddListener(() => SfxPlayer.Play(id));
            }
        }
    }
}
